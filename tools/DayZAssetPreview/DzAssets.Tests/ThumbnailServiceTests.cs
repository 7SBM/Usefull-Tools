using DzAssets.Formats.Models;
using DzAssets.Preview.Module.AssetVorschau;
using DzAssets.Preview.Shell;

namespace DzAssets.Tests;

public class ThumbnailServiceTests : IDisposable
{
    private readonly string _cache =
        Path.Combine(Path.GetTempPath(), "dzthumb_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_cache, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private ThumbnailService Dienst()
    {
        var protokoll = new Protokoll(Path.Combine(_cache, "log.txt"));
        var lader = new TexturLader(new TextureResolver(TestAssets.PDrive ?? @"C:\"), protokoll);
        return new ThumbnailService(_cache, lader, protokoll);
    }

    [PDriveFact]
    public void Der_Cachename_ist_fuer_dieselbe_Datei_stabil()
    {
        var pfad = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");
        var dienst = Dienst();

        var eins = dienst.CacheDatei(pfad);
        var zwei = dienst.CacheDatei(pfad);

        Assert.Equal(eins, zwei);
        Assert.EndsWith(".png", eins);
        Assert.StartsWith(_cache, eins);
    }

    [PDriveFact]
    public void Zwei_verschiedene_Modelle_bekommen_verschiedene_Cachenamen()
    {
        var dienst = Dienst();
        var a = TestAssets.ErsteDatei("structures", "*.p3d");
        var b = TestAssets.ErsteDatei("plants", "*.p3d");

        Assert.NotEqual(dienst.CacheDatei(a), dienst.CacheDatei(b));
    }

    [Fact]
    public void Ohne_vorhandenes_Bild_liefert_AusCache_null()
    {
        Assert.Null(Dienst().AusCache(@"C:\gibt-es-nicht\modell.p3d"));
    }

    [PDriveFact]
    public void Erzeugen_liefert_ein_Bild_der_erwarteten_Kantenlaenge_und_legt_es_ab()
    {
        // RenderTargetBitmap braucht einen STA-Thread; xUnit laeuft in MTA.
        var pfad = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");

        System.Windows.Media.Imaging.BitmapSource? ergebnis = null;
        string? cacheDatei = null;
        Exception? fehler = null;

        var faden = new Thread(() =>
        {
            try
            {
                var dienst = Dienst();
                cacheDatei = dienst.CacheDatei(pfad);
                ergebnis = dienst.Erzeugen(pfad);
            }
            catch (Exception ausnahme)
            {
                fehler = ausnahme;
            }
        });
        faden.SetApartmentState(ApartmentState.STA);
        faden.Start();
        faden.Join(TimeSpan.FromSeconds(60));

        Assert.Null(fehler);
        Assert.NotNull(ergebnis);
        Assert.Equal(ThumbnailService.Kante, ergebnis!.PixelWidth);
        Assert.Equal(ThumbnailService.Kante, ergebnis.PixelHeight);
        Assert.True(ergebnis.IsFrozen);
        Assert.True(File.Exists(cacheDatei), $"PNG nicht abgelegt: {cacheDatei}");
    }

    [PDriveFact]
    public void Ein_erzeugtes_Bild_wird_beim_naechsten_Mal_aus_dem_Cache_gelesen()
    {
        var pfad = TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d");

        System.Windows.Media.Imaging.BitmapSource? ausCache = null;
        Exception? fehler = null;

        var faden = new Thread(() =>
        {
            try
            {
                var dienst = Dienst();
                dienst.Erzeugen(pfad);
                ausCache = dienst.AusCache(pfad);
            }
            catch (Exception ausnahme)
            {
                fehler = ausnahme;
            }
        });
        faden.SetApartmentState(ApartmentState.STA);
        faden.Start();
        faden.Join(TimeSpan.FromSeconds(60));

        Assert.Null(fehler);
        Assert.NotNull(ausCache);
        Assert.Equal(ThumbnailService.Kante, ausCache!.PixelWidth);
    }

    [PDriveFact]
    public void Ein_Modell_ohne_sichtbaren_LOD_liefert_null_statt_zu_werfen()
    {
        // Ein Proxy-Modell hat haeufig keinen sichtbaren LOD.
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ");
        var ohneLod = Directory
            .EnumerateFiles(wurzel, "*.p3d", SearchOption.AllDirectories)
            .Take(400)
            .FirstOrDefault(p =>
            {
                try { return P3dModelReader.Read(p).FeinsterSichtbarerLod is null; }
                catch (Exception) { return false; }
            });

        if (ohneLod is null) return;   // keines gefunden: nichts zu pruefen

        Exception? fehler = null;
        System.Windows.Media.Imaging.BitmapSource? ergebnis = null;

        var faden = new Thread(() =>
        {
            try { ergebnis = Dienst().Erzeugen(ohneLod); }
            catch (Exception ausnahme) { fehler = ausnahme; }
        });
        faden.SetApartmentState(ApartmentState.STA);
        faden.Start();
        faden.Join(TimeSpan.FromSeconds(60));

        Assert.Null(fehler);
        Assert.Null(ergebnis);
    }
}
