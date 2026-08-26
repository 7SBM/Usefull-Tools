using System.IO;
using System.Text.Json;

namespace DzAssets.Preview.Shell;

public sealed class Einstellungen : BeobachtbaresObjekt
{
    private List<string> _wurzeln = [];
    private string? _zuletztGeoeffnet;
    private bool _bodengitterZeigen = true;
    private bool _massstabsfigurZeigen = true;
    private bool _drahtgitterZeigen;

    public List<string> Wurzeln
    {
        get => _wurzeln;
        set => Setzen(ref _wurzeln, value);
    }

    public string? ZuletztGeoeffnet
    {
        get => _zuletztGeoeffnet;
        set => Setzen(ref _zuletztGeoeffnet, value);
    }

    public bool BodengitterZeigen
    {
        get => _bodengitterZeigen;
        set => Setzen(ref _bodengitterZeigen, value);
    }

    public bool MassstabsfigurZeigen
    {
        get => _massstabsfigurZeigen;
        set => Setzen(ref _massstabsfigurZeigen, value);
    }

    public bool DrahtgitterZeigen
    {
        get => _drahtgitterZeigen;
        set => Setzen(ref _drahtgitterZeigen, value);
    }

    public static Einstellungen Laden(string datei)
    {
        Einstellungen? geladen = null;
        try
        {
            if (File.Exists(datei))
                geladen = JsonSerializer.Deserialize<Einstellungen>(File.ReadAllText(datei));
        }
        catch (Exception fehler)
            when (fehler is JsonException or IOException or UnauthorizedAccessException)
        {
            geladen = null;
        }

        geladen ??= new Einstellungen();
        geladen.Wurzeln ??= [];

        if (geladen.Wurzeln.Count == 0)
            geladen.Wurzeln = WurzelnErraten().ToList();

        return geladen;
    }

    public void Speichern(string datei)
    {
        try
        {
            var ordner = Path.GetDirectoryName(datei);
            if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);

            File.WriteAllText(datei,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Nicht speichern zu koennen darf die Anwendung nicht beenden.
        }
    }

    /// <summary>
    /// Sucht nach einem Arbeitslaufwerk mit entpackten Gamefiles.
    /// Erkennungsmerkmal ist ein Unterordner "DZ".
    /// </summary>
    public static IReadOnlyList<string> WurzelnErraten()
    {
        var kandidaten = new List<string> { @"H:\P_Drive", @"P:\", @"C:\P_Drive" };

        try
        {
            foreach (var laufwerk in DriveInfo.GetDrives())
            {
                if (!laufwerk.IsReady) continue;
                kandidaten.Add(Path.Combine(laufwerk.RootDirectory.FullName, "P_Drive"));
            }
        }
        catch (IOException)
        {
            // Laufwerksliste nicht abrufbar: die festen Kandidaten genuegen.
        }

        return kandidaten
            .Where(k => SicherVorhanden(Path.Combine(k, "DZ")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool SicherVorhanden(string pfad)
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
