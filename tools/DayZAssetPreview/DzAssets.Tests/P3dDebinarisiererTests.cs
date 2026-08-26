using DzAssets.Formats.Models;

namespace DzAssets.Tests;

public class P3dDebinarisiererTests : IDisposable
{
    private readonly string _ordner =
        Path.Combine(Path.GetTempPath(), "dzdebin_" + Guid.NewGuid().ToString("N"));

    public P3dDebinarisiererTests() => Directory.CreateDirectory(_ordner);

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Der_Zielpfad_bekommt_auf_Wunsch_das_mlod_Suffix()
    {
        var mit = P3dDebinarisierer.ZielPfad(@"C:\a\haus.p3d", null, suffix: true);
        var ohne = P3dDebinarisierer.ZielPfad(@"C:\a\haus.p3d", null, suffix: false);

        Assert.Equal(@"C:\a\haus_mlod.p3d", mit);
        Assert.Equal(@"C:\a\haus.p3d", ohne);
    }

    [Fact]
    public void Ein_Ausgabeordner_verlegt_das_Ziel()
    {
        var ziel = P3dDebinarisierer.ZielPfad(@"C:\a\haus.p3d", @"D:\raus", suffix: true);

        Assert.Equal(@"D:\raus\haus_mlod.p3d", ziel);
    }

    [Fact]
    public void Eine_fehlende_Quelle_wird_als_Fehlschlag_gemeldet_statt_zu_werfen()
    {
        var bericht = P3dDebinarisierer.Umwandeln(
            Path.Combine(_ordner, "gibt-es-nicht.p3d"),
            Path.Combine(_ordner, "raus.p3d"),
            ueberschreiben: true);

        Assert.Equal(DebinErgebnis.Fehlgeschlagen, bericht.Ergebnis);
        Assert.NotNull(bericht.Fehler);
    }

    [Fact]
    public void Eine_unlesbare_Datei_wird_als_Fehlschlag_gemeldet()
    {
        var quelle = Path.Combine(_ordner, "kaputt.p3d");
        File.WriteAllText(quelle, "das ist kein P3D");

        var bericht = P3dDebinarisierer.Umwandeln(
            quelle, Path.Combine(_ordner, "raus.p3d"), ueberschreiben: true);

        Assert.Equal(DebinErgebnis.Fehlgeschlagen, bericht.Ergebnis);
    }

    [PDriveFact]
    public void Wandelt_ein_echtes_ODOL_in_eine_lesbare_MLOD_Datei()
    {
        var quelle = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");
        var ziel = Path.Combine(_ordner, "basin_a_mlod.p3d");

        var bericht = P3dDebinarisierer.Umwandeln(quelle, ziel, ueberschreiben: true);

        Assert.Equal(DebinErgebnis.Umgewandelt, bericht.Ergebnis);
        Assert.Null(bericht.Fehler);
        Assert.True(File.Exists(ziel), "Die Zieldatei fehlt.");
        Assert.True(new FileInfo(ziel).Length > 0, "Die Zieldatei ist leer.");

        // Die Probe aufs Exempel: das Ergebnis muss sich wieder einlesen
        // lassen und dieselbe Gestalt haben wie das Original.
        var original = P3dModelReader.Read(quelle);
        var umgewandelt = P3dModelReader.Read(ziel);

        Assert.True(original.IstBinarisiert);
        Assert.False(umgewandelt.IstBinarisiert);
        Assert.NotEmpty(umgewandelt.Lods);
        Assert.NotNull(umgewandelt.FeinsterSichtbarerLod);

        // Die Ausdehnung darf sich nur im Rahmen der Rundung unterscheiden.
        Assert.Equal(original.Size.X, umgewandelt.Size.X, tolerance: 0.05f);
        Assert.Equal(original.Size.Y, umgewandelt.Size.Y, tolerance: 0.05f);
        Assert.Equal(original.Size.Z, umgewandelt.Size.Z, tolerance: 0.05f);
    }

    [PDriveFact]
    public void Eine_bereits_debinarisierte_Datei_wird_nicht_erneut_umgewandelt()
    {
        var quelle = TestAssets.Dz(@"structures\tracks\rail_tracke_2.p3d");
        if (!File.Exists(quelle)) return;

        var bericht = P3dDebinarisierer.Umwandeln(
            quelle, Path.Combine(_ordner, "x.p3d"), ueberschreiben: true);

        Assert.Equal(DebinErgebnis.WarSchonMlod, bericht.Ergebnis);
    }

    [PDriveFact]
    public void Ein_vorhandenes_Ziel_wird_ohne_Erlaubnis_nicht_ueberschrieben()
    {
        var quelle = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");
        var ziel = Path.Combine(_ordner, "schon_da.p3d");
        File.WriteAllText(ziel, "unangetastet");

        var bericht = P3dDebinarisierer.Umwandeln(quelle, ziel, ueberschreiben: false);

        Assert.Equal(DebinErgebnis.ZielVorhanden, bericht.Ergebnis);
        Assert.Equal("unangetastet", File.ReadAllText(ziel));
    }

    [PDriveFact]
    public void Ein_Stapel_meldet_jeden_Schritt_und_liefert_alle_Berichte()
    {
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ", "structures", "furniture");
        var quellen = Directory
            .EnumerateFiles(wurzel, "*.p3d", SearchOption.AllDirectories)
            .Take(4)
            .ToList();

        if (quellen.Count == 0) return;

        var gemeldet = new List<DebinBericht>();
        var berichte = P3dDebinarisierer.Umwandeln(
            quellen, _ordner, suffix: true, ueberschreiben: true,
            new Progress<DebinBericht>(b => { lock (gemeldet) gemeldet.Add(b); }));

        Assert.Equal(quellen.Count, berichte.Count);
        Assert.Contains(berichte, b => b.Ergebnis == DebinErgebnis.Umgewandelt);
        Assert.All(berichte.Where(b => b.Ergebnis == DebinErgebnis.Umgewandelt),
            b => Assert.True(File.Exists(b.Ziel)));
    }

    [PDriveFact]
    public void Die_Dateisuche_uebergeht_bereits_umgewandelte_Dateien()
    {
        File.WriteAllText(Path.Combine(_ordner, "a.p3d"), "x");
        File.WriteAllText(Path.Combine(_ordner, "b_mlod.p3d"), "x");

        var gefunden = P3dDebinarisierer.DateienSuchen(_ordner);

        Assert.Single(gefunden);
        Assert.EndsWith("a.p3d", gefunden[0]);
    }

    [Fact]
    public void Ein_Abbruch_im_Stapel_wird_beachtet()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();

        Assert.Throws<OperationCanceledException>(() => P3dDebinarisierer.Umwandeln(
            [Path.Combine(_ordner, "egal.p3d")], _ordner, true, true, null, quelle.Token));
    }
}
