using DzAssets.Formats.Hoehen;

namespace DzAssets.Tests;

/// <summary>
/// Prueft gegen die echten Hoehenkarten des Nutzers. Sind sie nicht da,
/// ueberspringen sich die Tests.
/// </summary>
public class AscEchteDateienTests
{
    private static readonly string[] Kandidaten =
    [
        @"H:\BrienZ_QGIS\gtt_export\gtt_heightmap.asc",
        @"H:\BrienZ_Meiringen_TerrainBuilderProjekt\source\TerrainBuilder\BrienZ_Meiringen_highmap.asc",
        @"H:\BrienzMeiringen_QGIS\gtt_export_16K\gtt_heightmap.asc",
    ];

    private static string? Groesste()
        => Kandidaten.FirstOrDefault(File.Exists);

    [Fact]
    public void Liest_eine_echte_4096er_Karte_in_unter_zwanzig_Sekunden()
    {
        var datei = Groesste();
        if (datei is null) return;   // keine Karte vorhanden

        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var raster = AscRaster.Laden(datei);
        uhr.Stop();

        Assert.Equal(4096, raster.Spalten);
        Assert.Equal(4096, raster.Zeilen);
        Assert.Equal(4096 * 4096, raster.Werte.Length);
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(20), $"Dauer {uhr.Elapsed}");
    }

    [Fact]
    public void Die_Hoehen_der_Brienz_Karte_liegen_im_Alpenbereich()
    {
        var datei = Groesste();
        if (datei is null) return;

        var raster = AscRaster.Laden(datei);

        // Brienzersee-Region: Seespiegel um 560 m, Gipfel bis gut 2300 m.
        // Waere der Punkt als Dezimaltrennzeichen falsch gelesen worden,
        // laegen hier sechsstellige Werte.
        Assert.InRange(raster.Min, 100f, 1500f);
        Assert.InRange(raster.Max, 1000f, 4500f);
        Assert.True(raster.Max > raster.Min);
    }

    [Fact]
    public void Der_Kartenursprung_ist_der_Terrain_Builder_Nullpunkt()
    {
        var datei = Groesste();
        if (datei is null) return;

        var raster = AscRaster.Laden(datei);

        // Alle 80 vorgefundenen Dateien nennen 200000 / 0 — das ist der
        // TB-Kartenursprung, keine echte Landeskoordinate.
        Assert.Equal(200000.0, raster.XEcke);
        Assert.Equal(0.0, raster.YEcke);
        Assert.True(raster.Zellgroesse > 0);
    }

    [Fact]
    public void Aus_einer_echten_Karte_entsteht_ein_plausibles_Reliefbild()
    {
        var datei = Groesste();
        if (datei is null) return;

        var raster = AscRaster.Laden(datei);
        var klein = Reliefbild.Verkleinern(raster, 512);

        Assert.True(klein.Spalten <= 512);
        Assert.True(klein.Zeilen <= 512);
        Assert.Equal(8, klein.Faktor);   // 4096 / 512

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Gelaende, raster.Zellgroesse);
        Assert.Equal(klein.Spalten * klein.Zeilen * 4, bild.Length);

        // Ein echtes Gelaende ist nicht einfarbig.
        var erstes = (bild[0], bild[1], bild[2]);
        var verschieden = false;
        for (var i = 4; i < bild.Length && !verschieden; i += 4)
            verschieden = (bild[i], bild[i + 1], bild[i + 2]) != erstes;

        Assert.True(verschieden, "Das Reliefbild ist einfarbig — da stimmt etwas nicht.");
    }
}
