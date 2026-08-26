using System.IO;
using System.Text.Json;

namespace DzAssets.Formats.Katalog;

/// <summary>
/// Verzeichnis aller .p3d-Modelle unterhalb der konfigurierten Wurzeln.
/// </summary>
public sealed class AssetIndex
{
    private sealed record CacheInhalt(string[] Wurzeln, AssetEintrag[] Eintraege);

    private AssetIndex(IReadOnlyList<AssetEintrag> eintraege, IReadOnlyList<string> wurzeln)
    {
        Eintraege = eintraege;
        Wurzeln = wurzeln;
    }

    public IReadOnlyList<AssetEintrag> Eintraege { get; }
    public IReadOnlyList<string> Wurzeln { get; }

    public static AssetIndex Erstellen(
        IReadOnlyList<string> wurzeln,
        IProgress<int>? fortschritt = null,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(wurzeln);

        var eintraege = new List<AssetEintrag>(16_384);

        var optionen = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var wurzel in wurzeln)
        {
            if (string.IsNullOrWhiteSpace(wurzel) || !VerzeichnisVorhanden(wurzel))
                continue;

            foreach (var datei in Directory.EnumerateFiles(wurzel, "*.p3d", optionen))
            {
                abbruch.ThrowIfCancellationRequested();

                long groesse;
                DateTime geaendert;
                try
                {
                    var info = new FileInfo(datei);
                    groesse = info.Length;
                    geaendert = info.LastWriteTimeUtc;
                }
                catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                var relativ = Path.GetRelativePath(wurzel, datei);
                eintraege.Add(new AssetEintrag(
                    AbsoluterPfad: datei,
                    AssetPfad: relativ,
                    Name: Path.GetFileNameWithoutExtension(datei),
                    Ordner: Path.GetDirectoryName(relativ) ?? string.Empty,
                    Groesse: groesse,
                    GeaendertUtc: geaendert));

                if (eintraege.Count % 500 == 0)
                    fortschritt?.Report(eintraege.Count);
            }
        }

        fortschritt?.Report(eintraege.Count);
        return new AssetIndex(eintraege, wurzeln.ToArray());
    }

    public static AssetIndex? AusCache(string cacheDatei, IReadOnlyList<string> wurzeln)
    {
        ArgumentNullException.ThrowIfNull(wurzeln);
        if (!File.Exists(cacheDatei)) return null;

        try
        {
            var inhalt = JsonSerializer.Deserialize<CacheInhalt>(File.ReadAllText(cacheDatei));
            if (inhalt?.Wurzeln is null || inhalt.Eintraege is null) return null;

            var gleich = inhalt.Wurzeln.Length == wurzeln.Count
                && inhalt.Wurzeln
                    .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(
                        wurzeln.OrderBy(w => w, StringComparer.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase);

            return gleich ? new AssetIndex(inhalt.Eintraege, inhalt.Wurzeln) : null;
        }
        catch (Exception fehler)
            when (fehler is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void InCacheSchreiben(string cacheDatei)
    {
        try
        {
            var ordner = Path.GetDirectoryName(cacheDatei);
            if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);

            var inhalt = new CacheInhalt(Wurzeln.ToArray(), Eintraege.ToArray());
            File.WriteAllText(cacheDatei, JsonSerializer.Serialize(inhalt));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Ein fehlender Cache kostet nur Zeit beim naechsten Start.
        }
    }

    /// <summary>
    /// Alle durch Leerzeichen getrennten Woerter muessen im Assetpfad
    /// vorkommen. Treffer, deren Dateiname mit dem ersten Wort beginnt,
    /// stehen vorn; danach kuerzere Namen vor laengeren.
    /// </summary>
    public IEnumerable<AssetEintrag> Suche(string text, int hoechstens = 500)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Eintraege.Take(hoechstens);

        var woerter = text.Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (woerter.Length == 0)
            return Eintraege.Take(hoechstens);

        var erstes = woerter[0];

        return Eintraege
            .Where(e => woerter.All(w => e.AssetPfad.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Name.StartsWith(erstes, StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => e.Name.Length)
            .ThenBy(e => e.AssetPfad, StringComparer.OrdinalIgnoreCase)
            .Take(hoechstens);
    }

    private static bool VerzeichnisVorhanden(string pfad)
    {
        try
        {
            return Directory.Exists(pfad);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
