namespace DzAssets.Tests;

/// <summary>
/// Zugriff auf die echten, entpackten Gamefiles. Fehlt das P-Drive
/// (z. B. auf einem Bau-Server), ueberspringen sich die betroffenen
/// Tests, statt fehlzuschlagen.
/// </summary>
public static class TestAssets
{
    private static readonly string[] Kandidaten =
    [
        @"H:\P_Drive",
        @"P:\",
    ];

    public static string? PDrive { get; } = Ermitteln();

    public static bool HasPDrive => PDrive is not null;

    private static string? Ermitteln()
    {
        var ausUmgebung = Environment.GetEnvironmentVariable("DZ_PDRIVE");
        if (!string.IsNullOrWhiteSpace(ausUmgebung)
            && Directory.Exists(Path.Combine(ausUmgebung, "DZ")))
        {
            return ausUmgebung;
        }

        return Kandidaten.FirstOrDefault(k => Directory.Exists(Path.Combine(k, "DZ")));
    }

    /// <summary>Absoluter Pfad zu einer Datei unterhalb von DZ\.</summary>
    public static string Dz(string relativePath)
    {
        if (PDrive is null)
            throw new InvalidOperationException("Kein P-Drive gefunden.");

        return Path.Combine(PDrive, "DZ", relativePath.Replace('/', '\\'));
    }

    /// <summary>Erste Datei mit dem gegebenen Muster unterhalb von DZ\unterordner.</summary>
    public static string ErsteDatei(string unterordner, string muster)
    {
        if (PDrive is null)
            throw new InvalidOperationException("Kein P-Drive gefunden.");

        var wurzel = Path.Combine(PDrive, "DZ", unterordner);
        return Directory.EnumerateFiles(wurzel, muster, SearchOption.AllDirectories).First();
    }
}

/// <summary>Ueberspringt einen Test, wenn das P-Drive nicht vorhanden ist.</summary>
public sealed class PDriveFactAttribute : FactAttribute
{
    public PDriveFactAttribute()
    {
        if (!TestAssets.HasPDrive)
            Skip = "P-Drive mit entpackten Gamefiles nicht gefunden.";
    }
}
