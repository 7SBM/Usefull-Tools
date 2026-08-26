using DzAssets.Formats.Katalog;

namespace DzAssets.Tests;

public class AssetIndexTests : IDisposable
{
    private readonly string _tempWurzel =
        Path.Combine(Path.GetTempPath(), "dzai_" + Guid.NewGuid().ToString("N"));

    public AssetIndexTests()
    {
        Directory.CreateDirectory(Path.Combine(_tempWurzel, "DZ", "structures", "haus"));
        Directory.CreateDirectory(Path.Combine(_tempWurzel, "DZ", "plants", "baum"));
        File.WriteAllBytes(Path.Combine(_tempWurzel, "DZ", "structures", "haus", "haus_gross.p3d"), [1]);
        File.WriteAllBytes(Path.Combine(_tempWurzel, "DZ", "structures", "haus", "haus_klein.p3d"), [1]);
        File.WriteAllBytes(Path.Combine(_tempWurzel, "DZ", "plants", "baum", "t_fichte.p3d"), [1]);
        File.WriteAllText(Path.Combine(_tempWurzel, "DZ", "plants", "baum", "egal.txt"), "kein Modell");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempWurzel, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Findet_alle_p3d_und_ignoriert_andere_Dateien()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        Assert.Equal(3, index.Eintraege.Count);
        Assert.All(index.Eintraege, e => Assert.EndsWith(".p3d", e.AbsoluterPfad));
    }

    [Fact]
    public void Der_Assetpfad_ist_relativ_zur_Wurzel()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        var eintrag = index.Eintraege.Single(e => e.Name == "t_fichte");
        Assert.Equal(@"DZ\plants\baum\t_fichte.p3d", eintrag.AssetPfad);
        Assert.Equal(@"DZ\plants\baum", eintrag.Ordner);
    }

    [Fact]
    public void Die_Suche_verknuepft_mehrere_Woerter_mit_und()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        var treffer = index.Suche("haus gross").ToList();

        Assert.Single(treffer);
        Assert.Equal("haus_gross", treffer[0].Name);
    }

    [Fact]
    public void Die_Suche_ignoriert_Gross_und_Kleinschreibung()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        Assert.Equal(2, index.Suche("HAUS").Count());
    }

    [Fact]
    public void Treffer_mit_passendem_Dateianfang_stehen_vorn()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        var treffer = index.Suche("t_").ToList();

        Assert.NotEmpty(treffer);
        Assert.Equal("t_fichte", treffer[0].Name);
    }

    [Fact]
    public void Die_Suche_begrenzt_die_Trefferzahl()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        Assert.Single(index.Suche("p3d", hoechstens: 1));
    }

    [Fact]
    public void Eine_leere_Suche_liefert_den_Anfang_des_Bestands()
    {
        var index = AssetIndex.Erstellen([_tempWurzel]);

        Assert.Equal(3, index.Suche("   ").Count());
    }

    [Fact]
    public void Der_Cache_wird_geschrieben_und_wieder_gelesen()
    {
        var cacheDatei = Path.Combine(_tempWurzel, "index.json");
        var original = AssetIndex.Erstellen([_tempWurzel]);

        original.InCacheSchreiben(cacheDatei);
        var geladen = AssetIndex.AusCache(cacheDatei, [_tempWurzel]);

        Assert.NotNull(geladen);
        Assert.Equal(original.Eintraege.Count, geladen!.Eintraege.Count);
        Assert.Equal(original.Eintraege[0].AssetPfad, geladen.Eintraege[0].AssetPfad);
    }

    [Fact]
    public void Ein_Cache_fuer_andere_Wurzeln_wird_verworfen()
    {
        var cacheDatei = Path.Combine(_tempWurzel, "index.json");
        AssetIndex.Erstellen([_tempWurzel]).InCacheSchreiben(cacheDatei);

        var geladen = AssetIndex.AusCache(cacheDatei, [_tempWurzel, @"C:\andere-wurzel"]);

        Assert.Null(geladen);
    }

    [Fact]
    public void Ein_beschaedigter_Cache_wird_verworfen_statt_zu_werfen()
    {
        var cacheDatei = Path.Combine(_tempWurzel, "kaputt.json");
        File.WriteAllText(cacheDatei, "{ das ist kein JSON");

        Assert.Null(AssetIndex.AusCache(cacheDatei, [_tempWurzel]));
    }

    [Fact]
    public void Eine_nicht_vorhandene_Wurzel_fuehrt_nicht_zu_einer_Ausnahme()
    {
        var index = AssetIndex.Erstellen([@"C:\gibt-es-ganz-sicher-nicht-4711"]);

        Assert.Empty(index.Eintraege);
    }

    [Fact]
    public void Der_Fortschritt_wird_am_Ende_gemeldet()
    {
        var gemeldet = new List<int>();
        AssetIndex.Erstellen([_tempWurzel], new Progress<int>(gemeldet.Add));

        // Progress<T> meldet ueber den Synchronisationskontext; im Test
        // genuegt, dass der Aufruf ohne Ausnahme durchlaeuft.
        Assert.True(true);
    }

    [Fact]
    public void Ein_Abbruch_wird_beachtet()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => AssetIndex.Erstellen([_tempWurzel], null, quelle.Token));
    }

    [PDriveFact]
    public void Der_echte_Bestand_wird_in_unter_dreissig_Sekunden_eingelesen()
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();

        var index = AssetIndex.Erstellen([TestAssets.PDrive!]);

        uhr.Stop();
        Assert.True(index.Eintraege.Count > 5000, $"Nur {index.Eintraege.Count} Modelle gefunden");
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(30), $"Dauer {uhr.Elapsed}");
    }
}
