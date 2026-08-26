using System.IO;
using System.Text.RegularExpressions;

namespace DzAssets.Formats.Katalog;

/// <summary>
/// Ordnet Modellpfaden die Klassennamen aus den config.cpp-Dateien zu.
/// Bewusst ein toleranter Zeilenscanner und kein vollstaendiger Parser:
/// die Spieldateien enthalten Makros und #include, die ein strenger Parser
/// ablehnen wuerde. Ein falsch zugeordneter Name ist hier folgenlos — die
/// Angabe ist ein Hinweis im Info-Panel, keine Entscheidungsgrundlage.
/// </summary>
public sealed partial class ConfigClassIndex
{
    private readonly Dictionary<string, List<string>> _nachModell =
        new(StringComparer.OrdinalIgnoreCase);

    public int AnzahlKlassen => _nachModell.Values.Sum(l => l.Count);
    public int AnzahlModelle => _nachModell.Count;

    public IReadOnlyList<string> KlassenFuer(string assetPfad)
        => _nachModell.TryGetValue(Normalisieren(assetPfad), out var liste)
            ? liste
            : [];

    public static ConfigClassIndex AusText(string configText)
    {
        var index = new ConfigClassIndex();
        index.Verarbeiten(configText);
        return index;
    }

    public static ConfigClassIndex Erstellen(
        IReadOnlyList<string> wurzeln,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(wurzeln);
        var index = new ConfigClassIndex();

        var optionen = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var wurzel in wurzeln)
        {
            if (string.IsNullOrWhiteSpace(wurzel) || !Directory.Exists(wurzel)) continue;

            foreach (var datei in Directory.EnumerateFiles(wurzel, "config.cpp", optionen))
            {
                abbruch.ThrowIfCancellationRequested();
                try
                {
                    index.Verarbeiten(File.ReadAllText(datei));
                }
                catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
                {
                    // Eine unlesbare config.cpp darf den Rest nicht verhindern.
                }
            }
        }

        return index;
    }

    /// <summary>
    /// Ein einziger Durchlauf in Textreihenfolge. Zeilenweise zu arbeiten
    /// waere falsch: bei einer einzeiligen Klasse — «class X { model="…"; };» —
    /// waere der Klassenstapel beim Erreichen von model= bereits wieder
    /// abgebaut, und die Zuordnung ginge verloren.
    /// </summary>
    private void Verarbeiten(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        text = BlockKommentar().Replace(text, " ");
        text = ZeilenKommentar().Replace(text, string.Empty);

        var stapel = new Stack<string>();
        string? offenerName = null;

        foreach (Match treffer in Ereignis().Matches(text))
        {
            if (treffer.Groups["model"].Success)
            {
                Zuordnen(treffer.Groups["pfad"].Value);
                continue;
            }

            // Zeichenketten werden uebersprungen, damit Klammern oder
            // Semikola in Anzeigenamen den Stapel nicht verfaelschen.
            if (treffer.Groups["text"].Success) continue;

            if (treffer.Groups["klasse"].Success)
            {
                offenerName = treffer.Groups["name"].Value;
            }
            else if (treffer.Groups["auf"].Success)
            {
                stapel.Push(offenerName ?? string.Empty);
                offenerName = null;
            }
            else if (treffer.Groups["zu"].Success)
            {
                if (stapel.Count > 0) stapel.Pop();
            }
            else if (treffer.Groups["semi"].Success)
            {
                // "class Basis;" ohne Rumpf: der gemerkte Name verfaellt.
                offenerName = null;
            }
        }

        void Zuordnen(string modellPfad)
        {
            if (stapel.Count == 0) return;

            var klasse = stapel.Peek();
            if (klasse.Length == 0) return;

            var schluessel = Normalisieren(modellPfad);
            if (schluessel.Length == 0) return;

            if (!_nachModell.TryGetValue(schluessel, out var liste))
                _nachModell[schluessel] = liste = [];

            if (!liste.Contains(klasse, StringComparer.OrdinalIgnoreCase))
                liste.Add(klasse);
        }
    }

    private static string Normalisieren(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) return string.Empty;

        var wert = pfad.Trim().Replace('/', '\\').TrimStart('\\');
        if (wert.Length == 0) return string.Empty;
        if (!wert.EndsWith(".p3d", StringComparison.OrdinalIgnoreCase)) wert += ".p3d";
        return wert.ToLowerInvariant();
    }

    /// <summary>
    /// Alle Ereignisse in einem Ausdruck, damit sie in Textreihenfolge
    /// ankommen. Die Reihenfolge der Alternativen ist bedeutsam: model
    /// zuerst (spezifischer als eine beliebige Zeichenkette), danach
    /// Zeichenketten, damit deren Inhalt nichts ausloest.
    /// </summary>
    [GeneratedRegex(
        """(?<model>\bmodel\s*=\s*"(?<pfad>[^"]*)"\s*;)|(?<text>"[^"]*")|(?<klasse>\bclass\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))|(?<auf>\{)|(?<zu>\})|(?<semi>;)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Ereignis();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockKommentar();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex ZeilenKommentar();
}
