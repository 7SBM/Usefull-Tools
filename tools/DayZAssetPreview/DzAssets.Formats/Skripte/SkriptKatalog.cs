using System.IO;

namespace DzAssets.Formats.Skripte;

/// <summary>
/// Findet Hilfsskripte in den eingestellten Ordnern.
/// </summary>
public sealed class SkriptKatalog
{
    private static readonly string[] Endungen = [".py", ".ps1", ".bat", ".cmd"];

    private SkriptKatalog(IReadOnlyList<SkriptEintrag> eintraege, IReadOnlyList<string> ordner)
    {
        Eintraege = eintraege;
        Ordner = ordner;
    }

    public IReadOnlyList<SkriptEintrag> Eintraege { get; }
    public IReadOnlyList<string> Ordner { get; }

    public static SkriptKatalog Erstellen(IReadOnlyList<string> ordner,
                                          bool mitUnterordnern = true,
                                          CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(ordner);

        var eintraege = new List<SkriptEintrag>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var optionen = new EnumerationOptions
        {
            RecurseSubdirectories = mitUnterordnern,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var wurzel in ordner)
        {
            if (string.IsNullOrWhiteSpace(wurzel) || !Directory.Exists(wurzel)) continue;

            foreach (var datei in Directory.EnumerateFiles(wurzel, "*", optionen))
            {
                abbruch.ThrowIfCancellationRequested();

                var endung = Path.GetExtension(datei).ToLowerInvariant();
                if (!Endungen.Contains(endung)) continue;

                // Bauergebnisse und Fremdpakete gehoeren nicht in die Liste.
                if (LiegtInAusgeschlossenemOrdner(datei)) continue;
                if (!gesehen.Add(datei)) continue;

                try
                {
                    eintraege.Add(SkriptEintrag.AusDatei(datei));
                }
                catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
                {
                    // Ein unlesbares Skript darf den Rest nicht verhindern.
                }
            }
        }

        eintraege.Sort((a, b) =>
        {
            var nachOrdner = string.Compare(a.Ordner, b.Ordner, StringComparison.OrdinalIgnoreCase);
            return nachOrdner != 0
                ? nachOrdner
                : string.Compare(a.Dateiname, b.Dateiname, StringComparison.OrdinalIgnoreCase);
        });

        return new SkriptKatalog(eintraege, ordner.ToArray());
    }

    private static bool LiegtInAusgeschlossenemOrdner(string pfad)
    {
        var teile = pfad.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var teil in teile)
        {
            if (teil.Equals("bin", StringComparison.OrdinalIgnoreCase)) return true;
            if (teil.Equals("obj", StringComparison.OrdinalIgnoreCase)) return true;
            if (teil.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) return true;
            if (teil.Equals(".git", StringComparison.OrdinalIgnoreCase)) return true;
            if (teil.Equals("veroeffentlicht", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>Sucht in Dateiname, Titel und Beschreibung.</summary>
    public IEnumerable<SkriptEintrag> Suche(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Eintraege;

        var woerter = text.Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Eintraege.Where(e => woerter.All(w =>
            e.Dateiname.Contains(w, StringComparison.OrdinalIgnoreCase)
            || e.Titel.Contains(w, StringComparison.OrdinalIgnoreCase)
            || e.Beschreibung.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }
}
