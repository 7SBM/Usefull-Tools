using System.IO;
using System.Text;

namespace DzAssets.Formats.Skripte;

public enum SkriptArt
{
    Python,
    PowerShell,
    Batch,
    Unbekannt,
}

/// <summary>
/// Ein gefundenes Hilfsskript samt der Beschreibung aus seinem Kopf.
/// </summary>
public sealed class SkriptEintrag
{
    public required string Pfad { get; init; }
    public required string Dateiname { get; init; }
    public required SkriptArt Art { get; init; }

    /// <summary>Erste Zeile der Kopfbeschreibung, sonst der Dateiname.</summary>
    public required string Titel { get; init; }

    /// <summary>Vollstaendiger Kopfkommentar, ohne die Kommentarzeichen.</summary>
    public required string Beschreibung { get; init; }

    public required long Groesse { get; init; }
    public required DateTime GeaendertUtc { get; init; }

    /// <summary>Ordner, in dem das Skript liegt — dient als Gruppierung.</summary>
    public required string Ordner { get; init; }

    public string ArtName => Art switch
    {
        SkriptArt.Python => "Python",
        SkriptArt.PowerShell => "PowerShell",
        SkriptArt.Batch => "Batch",
        _ => "unbekannt",
    };

    public static SkriptArt ArtAusEndung(string pfad) =>
        Path.GetExtension(pfad).ToLowerInvariant() switch
        {
            ".py" => SkriptArt.Python,
            ".ps1" => SkriptArt.PowerShell,
            ".bat" or ".cmd" => SkriptArt.Batch,
            _ => SkriptArt.Unbekannt,
        };

    /// <summary>
    /// Zieht den Kopfkommentar heraus. Python nutzt einen Docstring,
    /// PowerShell und Batch nutzen fuehrende Kommentarzeilen — beide
    /// Formen kommen in den Skripten des Anwenders vor.
    /// </summary>
    public static string BeschreibungAusText(string text, SkriptArt art)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var zeilen = text.Replace("\r\n", "\n").Split('\n');
        return art == SkriptArt.Python
            ? AusPython(zeilen)
            : AusKommentarzeilen(zeilen, art);
    }

    private static string AusPython(string[] zeilen)
    {
        var i = 0;

        // Shebang und Kodierungszeile ueberspringen.
        while (i < zeilen.Length
               && (zeilen[i].StartsWith("#!", StringComparison.Ordinal)
                   || zeilen[i].Contains("coding:", StringComparison.OrdinalIgnoreCase)
                   || zeilen[i].Trim().Length == 0))
        {
            i++;
        }

        if (i >= zeilen.Length) return string.Empty;

        var beginn = zeilen[i].TrimStart();
        var zeichen = beginn.StartsWith("\"\"\"", StringComparison.Ordinal) ? "\"\"\""
                    : beginn.StartsWith("'''", StringComparison.Ordinal) ? "'''"
                    : null;

        if (zeichen is null)
        {
            // Kein Docstring: dann die fuehrenden Rautenzeilen nehmen.
            return AusKommentarzeilen(zeilen[i..], SkriptArt.Python);
        }

        var sammler = new StringBuilder();
        var rest = beginn[zeichen.Length..];

        // Einzeiler: """Text"""
        if (rest.Contains(zeichen, StringComparison.Ordinal))
            return rest[..rest.IndexOf(zeichen, StringComparison.Ordinal)].Trim();

        if (rest.Trim().Length > 0) sammler.AppendLine(rest.Trim());

        for (i++; i < zeilen.Length; i++)
        {
            var zeile = zeilen[i];
            var ende = zeile.IndexOf(zeichen, StringComparison.Ordinal);
            if (ende >= 0)
            {
                if (ende > 0) sammler.AppendLine(zeile[..ende].TrimEnd());
                break;
            }

            sammler.AppendLine(zeile.TrimEnd());
        }

        return sammler.ToString().Trim();
    }

    private static string AusKommentarzeilen(string[] zeilen, SkriptArt art)
    {
        var sammler = new StringBuilder();
        var begonnen = false;

        foreach (var roh in zeilen)
        {
            var zeile = roh.Trim();

            if (zeile.Length == 0)
            {
                if (begonnen) break;
                continue;
            }

            string? inhalt = null;

            if (zeile.StartsWith('#')) inhalt = zeile.TrimStart('#');
            else if (zeile.StartsWith("::", StringComparison.Ordinal)) inhalt = zeile[2..];
            else if (zeile.StartsWith("REM ", StringComparison.OrdinalIgnoreCase)) inhalt = zeile[4..];
            else if (zeile.StartsWith("@REM ", StringComparison.OrdinalIgnoreCase)) inhalt = zeile[5..];

            if (inhalt is null) break;

            begonnen = true;

            // Zierlinien aus ===, --- oder *** tragen nichts bei.
            var bereinigt = inhalt.Trim().Trim('=', '-', '*', '_').Trim();
            if (bereinigt.Length == 0) continue;

            sammler.AppendLine(bereinigt);
        }

        _ = art;
        return sammler.ToString().Trim();
    }

    public static SkriptEintrag AusDatei(string pfad)
    {
        ArgumentException.ThrowIfNullOrEmpty(pfad);

        var art = ArtAusEndung(pfad);
        var beschreibung = string.Empty;

        try
        {
            // Der Kopf steht am Anfang; die ganze Datei zu lesen waere
            // bei einem grossen Skript unnoetig.
            using var leser = new StreamReader(pfad);
            var puffer = new char[4096];
            var gelesen = leser.Read(puffer, 0, puffer.Length);
            beschreibung = BeschreibungAusText(new string(puffer, 0, gelesen), art);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Ohne Beschreibung bleibt immer noch der Dateiname.
        }

        var info = new FileInfo(pfad);
        var titel = beschreibung.Length == 0
            ? Path.GetFileName(pfad)
            : beschreibung.Split('\n')[0].Trim();

        return new SkriptEintrag
        {
            Pfad = pfad,
            Dateiname = Path.GetFileName(pfad),
            Art = art,
            Titel = titel.Length == 0 ? Path.GetFileName(pfad) : titel,
            Beschreibung = beschreibung,
            Groesse = info.Exists ? info.Length : 0,
            GeaendertUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
            Ordner = Path.GetDirectoryName(pfad) ?? string.Empty,
        };
    }
}
