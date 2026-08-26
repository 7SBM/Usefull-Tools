using System.IO;

namespace DzAssets.Preview.Shell;

/// <summary>
/// Die Dienste, die sich alle Werkzeuge teilen. Als ein Objekt und nicht
/// als drei Konstruktorparameter, damit spaetere Module ohne Aenderung an
/// der Schnittstelle weitere Dienste vorfinden.
/// </summary>
public sealed class WerkzeugKontext(string datenOrdner, Einstellungen einstellungen, Protokoll protokoll)
{
    public const string EinstellungsDatei = "settings.json";

    public string DatenOrdner { get; } = datenOrdner;
    public Einstellungen Einstellungen { get; } = einstellungen;
    public Protokoll Protokoll { get; } = protokoll;

    /// <summary>Pfad einer Datei im Datenordner der Anwendung.</summary>
    public string DatenDatei(string name) => Path.Combine(DatenOrdner, name);

    public static WerkzeugKontext Standard()
    {
        var ordner = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "7SBM-DayZ-Tools");

        try
        {
            Directory.CreateDirectory(ordner);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            ordner = Path.Combine(Path.GetTempPath(), "7SBM-DayZ-Tools");
            Directory.CreateDirectory(ordner);
        }

        var einstellungen = Einstellungen.Laden(Path.Combine(ordner, EinstellungsDatei));
        var protokoll = new Protokoll(Path.Combine(ordner, "log.txt"));
        return new WerkzeugKontext(ordner, einstellungen, protokoll);
    }

    public void EinstellungenSpeichern()
        => Einstellungen.Speichern(DatenDatei(EinstellungsDatei));
}
