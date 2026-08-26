using DzAssets.Preview.Shell;

namespace DzAssets.Tests;

public class EinstellungenTests : IDisposable
{
    private readonly string _datei = Path.Combine(Path.GetTempPath(), $"dz_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_datei); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Eine_fehlende_Datei_ergibt_Voreinstellungen_statt_einer_Ausnahme()
    {
        var einstellungen = Einstellungen.Laden(_datei);

        Assert.NotNull(einstellungen);
        Assert.True(einstellungen.BodengitterZeigen);
        Assert.True(einstellungen.MassstabsfigurZeigen);
        Assert.False(einstellungen.DrahtgitterZeigen);
    }

    [Fact]
    public void Gespeicherte_Werte_werden_wieder_gelesen()
    {
        var original = Einstellungen.Laden(_datei);
        original.Wurzeln = [@"H:\P_Drive", @"D:\Mods"];
        original.BodengitterZeigen = false;
        original.ZuletztGeoeffnet = @"DZ\a\b.p3d";
        original.Speichern(_datei);

        var geladen = Einstellungen.Laden(_datei);

        Assert.Equal(2, geladen.Wurzeln.Count);
        Assert.Contains(@"D:\Mods", geladen.Wurzeln);
        Assert.False(geladen.BodengitterZeigen);
        Assert.Equal(@"DZ\a\b.p3d", geladen.ZuletztGeoeffnet);
    }

    [Fact]
    public void Eine_beschaedigte_Datei_fuehrt_zu_Voreinstellungen()
    {
        File.WriteAllText(_datei, "{ das ist kein JSON");

        var einstellungen = Einstellungen.Laden(_datei);

        Assert.NotNull(einstellungen);
        Assert.True(einstellungen.BodengitterZeigen);
    }

    [Fact]
    public void Aenderungen_werden_gemeldet()
    {
        var einstellungen = Einstellungen.Laden(_datei);
        var gemeldet = new List<string?>();
        einstellungen.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName);

        einstellungen.BodengitterZeigen = !einstellungen.BodengitterZeigen;

        Assert.Contains(nameof(Einstellungen.BodengitterZeigen), gemeldet);
    }

    [Fact]
    public void Ein_gleicher_Wert_loest_keine_Meldung_aus()
    {
        var einstellungen = Einstellungen.Laden(_datei);
        var gemeldet = 0;
        einstellungen.PropertyChanged += (_, _) => gemeldet++;

        einstellungen.BodengitterZeigen = einstellungen.BodengitterZeigen;

        Assert.Equal(0, gemeldet);
    }

    [Fact]
    public void Das_Erraten_der_Wurzeln_liefert_nur_vorhandene_Ordner()
    {
        var wurzeln = Einstellungen.WurzelnErraten();

        Assert.All(wurzeln, w => Assert.True(Directory.Exists(w), $"Nicht vorhanden: {w}"));
    }

    [PDriveFact]
    public void Das_Erraten_findet_das_vorhandene_Arbeitslaufwerk()
    {
        var wurzeln = Einstellungen.WurzelnErraten();

        Assert.Contains(wurzeln, w => Directory.Exists(Path.Combine(w, "DZ")));
    }
}

public class ProtokollTests : IDisposable
{
    private readonly string _datei =
        Path.Combine(Path.GetTempPath(), $"dzlog_{Guid.NewGuid():N}.txt");

    public void Dispose()
    {
        try { File.Delete(_datei); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Schreibt_eine_Zeile_je_Meldung()
    {
        var protokoll = new Protokoll(_datei);

        protokoll.Schreiben("erste");
        protokoll.Schreiben("zweite");

        var zeilen = File.ReadAllLines(_datei);
        Assert.Equal(2, zeilen.Length);
        Assert.Contains("erste", zeilen[0]);
        Assert.Contains("INFO", zeilen[0]);
    }

    [Fact]
    public void Ein_Fehler_wird_als_solcher_gekennzeichnet()
    {
        var protokoll = new Protokoll(_datei);

        protokoll.Fehler("ging schief", new InvalidOperationException("Grund"));

        var inhalt = File.ReadAllText(_datei);
        Assert.Contains("FEHLER", inhalt);
        Assert.Contains("ging schief", inhalt);
        Assert.Contains("Grund", inhalt);
    }

    [Fact]
    public void Der_TextWriter_schreibt_zeilenweise_ins_Protokoll()
    {
        var protokoll = new Protokoll(_datei);
        var schreiber = protokoll.AlsTextWriter();

        schreiber.WriteLine("Meldung aus BisDll");
        schreiber.WriteLine("noch eine");
        schreiber.Flush();

        var zeilen = File.ReadAllLines(_datei);
        Assert.Equal(2, zeilen.Length);
        Assert.Contains("BISDLL", zeilen[0]);
        Assert.Contains("Meldung aus BisDll", zeilen[0]);
    }

    [Fact]
    public void Ein_nicht_beschreibbarer_Pfad_wirft_nicht()
    {
        var protokoll = new Protokoll(@"Z:\gibt-es-nicht\log.txt");

        // Darf keine Ausnahme werfen.
        protokoll.Schreiben("egal");
        protokoll.Fehler("auch egal");
    }
}
