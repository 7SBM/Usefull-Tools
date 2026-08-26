using System.Globalization;
using System.Text;
using DzAssets.Formats.Hoehen;

namespace DzAssets.Tests;

public class AscRasterTests
{
    private static Stream Strom(string inhalt)
        => new MemoryStream(Encoding.ASCII.GetBytes(inhalt));

    /// <summary>Kopf wie im QGIS-Export: auf Spalte 15 aufgefuellt, CRLF.</summary>
    private const string QgisKopf =
        "ncols         3\r\n" +
        "nrows         2\r\n" +
        "xllcorner     200000.000000\r\n" +
        "yllcorner     0.000000\r\n" +
        "cellsize      5.0\r\n" +
        "NODATA_value  -9999.0\r\n";

    [Fact]
    public void Liest_Kopf_und_Werte_eines_QGIS_Exports()
    {
        var raster = AscRaster.Laden(Strom(QgisKopf + "1.5 2.5 3.5\r\n4.5 5.5 6.5\r\n"));

        Assert.Equal(3, raster.Spalten);
        Assert.Equal(2, raster.Zeilen);
        Assert.Equal(200000.0, raster.XEcke);
        Assert.Equal(0.0, raster.YEcke);
        Assert.Equal(5.0, raster.Zellgroesse);
        Assert.Equal(6, raster.Werte.Length);
        Assert.Equal(1.5f, raster[0, 0]);
        Assert.Equal(3.5f, raster[2, 0]);
        Assert.Equal(4.5f, raster[0, 1]);
        Assert.Equal(6.5f, raster[2, 1]);
    }

    [Fact]
    public void Der_Punkt_bleibt_Dezimaltrennzeichen_auch_bei_deutschem_Gebietsschema()
    {
        // Der wahrscheinlichste stille Fehler dieses Moduls: auf einem
        // deutschen System ergaebe float.Parse("1358.84") sonst 135884.
        var vorher = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var raster = AscRaster.Laden(Strom(QgisKopf + "1358.84 1359.59 1360.28\n1.0 2.0 3.0\n"));

            Assert.Equal(1358.84f, raster[0, 0], precision: 2);
            Assert.Equal(1360.28f, raster[2, 0], precision: 2);
        }
        finally
        {
            CultureInfo.CurrentCulture = vorher;
        }
    }

    [Fact]
    public void Kommt_mit_LF_und_mit_CRLF_zurecht()
    {
        var mitLf = QgisKopf.Replace("\r\n", "\n") + "1 2 3\n4 5 6\n";

        var raster = AscRaster.Laden(Strom(mitLf));

        Assert.Equal(6, raster.Werte.Length);
        Assert.Equal(6f, raster[2, 1]);
    }

    [Fact]
    public void Kommt_mit_einfachem_Leerzeichen_im_Kopf_zurecht()
    {
        // So schreibt das eigene Python-Skript des Nutzers.
        const string kopf =
            "ncols 3\nnrows 2\nxllcorner 200000.000000\nyllcorner 0.000000\n"
            + "cellsize 10.000000\nNODATA_value -9999.000000\n";

        var raster = AscRaster.Laden(Strom(kopf + "1.0 2.0 3.0\n4.0 5.0 6.0\n"));

        Assert.Equal(10.0, raster.Zellgroesse);
        Assert.Equal(3, raster.Spalten);
    }

    [Fact]
    public void Minimum_und_Maximum_lassen_NoData_aus()
    {
        var raster = AscRaster.Laden(Strom(QgisKopf + "10 -9999 30\n40 50 -9999.0\n"));

        Assert.Equal(10f, raster.Min);
        Assert.Equal(50f, raster.Max);
        Assert.Equal(2, raster.NoDataAnzahl);
    }

    [Fact]
    public void NoData_wird_numerisch_erkannt_egal_wie_geschrieben()
    {
        // -9999, -9999.0 und -9999.000000 kommen nebeneinander vor.
        var raster = AscRaster.Laden(Strom(QgisKopf + "-9999 -9999.0 -9999.000000\n1 2 3\n"));

        Assert.Equal(3, raster.NoDataAnzahl);
        Assert.Equal(1f, raster.Min);
    }

    [Fact]
    public void Ein_Raster_ganz_ohne_gueltige_Werte_liefert_null_als_Spanne()
    {
        var raster = AscRaster.Laden(Strom(QgisKopf + "-9999 -9999 -9999\n-9999 -9999 -9999\n"));

        Assert.Equal(0f, raster.Min);
        Assert.Equal(0f, raster.Max);
        Assert.Equal(6, raster.NoDataAnzahl);
    }

    [Fact]
    public void Fehlt_NODATA_value_wird_die_erste_Datenzeile_trotzdem_gelesen()
    {
        const string kopf =
            "ncols 3\nnrows 2\nxllcorner 0\nyllcorner 0\ncellsize 1\n";

        var raster = AscRaster.Laden(Strom(kopf + "1 2 3\n4 5 6\n"));

        Assert.Equal(6, raster.Werte.Length);
        Assert.Equal(1f, raster[0, 0]);
        Assert.Equal(6f, raster[2, 1]);
    }

    [Fact]
    public void Die_Ausdehnung_in_Metern_ergibt_sich_aus_Spalten_mal_Zellgroesse()
    {
        var raster = AscRaster.Laden(Strom(QgisKopf + "1 2 3\n4 5 6\n"));

        Assert.Equal(15.0, raster.BreiteMeter);
        Assert.Equal(10.0, raster.HoeheMeter);
    }

    [Fact]
    public void Zu_wenige_Werte_in_einer_Zeile_werden_abgewiesen()
    {
        var fehler = Assert.Throws<InvalidDataException>(
            () => AscRaster.Laden(Strom(QgisKopf + "1 2\n4 5 6\n")));

        Assert.Contains("2 Werte", fehler.Message);
    }

    [Fact]
    public void Zu_viele_Werte_in_einer_Zeile_werden_abgewiesen()
    {
        Assert.Throws<InvalidDataException>(
            () => AscRaster.Laden(Strom(QgisKopf + "1 2 3 4\n4 5 6\n")));
    }

    [Fact]
    public void Eine_zu_kurze_Datei_wird_abgewiesen()
    {
        var fehler = Assert.Throws<InvalidDataException>(
            () => AscRaster.Laden(Strom(QgisKopf + "1 2 3\n")));

        Assert.Contains("endet", fehler.Message);
    }

    [Fact]
    public void Ein_unbrauchbarer_Wert_nennt_Zeile_und_Stelle()
    {
        var fehler = Assert.Throws<InvalidDataException>(
            () => AscRaster.Laden(Strom(QgisKopf + "1 zwei 3\n4 5 6\n")));

        Assert.Contains("Zeile 1", fehler.Message);
        Assert.Contains("zwei", fehler.Message);
    }

    [Fact]
    public void Ein_fehlender_Kopf_wird_abgewiesen()
    {
        Assert.Throws<InvalidDataException>(() => AscRaster.Laden(Strom("1 2 3\n")));
    }

    [Fact]
    public void Ein_uebergrosses_Raster_wird_abgewiesen()
    {
        const string kopf =
            "ncols 99999\nnrows 99999\nxllcorner 0\nyllcorner 0\ncellsize 1\n";

        var fehler = Assert.Throws<InvalidDataException>(() => AscRaster.Laden(Strom(kopf)));

        Assert.Contains("gross", fehler.Message);
    }

    [Fact]
    public void Ein_Abbruch_wird_beachtet()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => AscRaster.Laden(Strom(QgisKopf + "1 2 3\n4 5 6\n"), null, quelle.Token));
    }
}
