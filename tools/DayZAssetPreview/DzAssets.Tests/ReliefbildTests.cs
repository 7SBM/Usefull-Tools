using System.Text;
using DzAssets.Formats.Hoehen;

namespace DzAssets.Tests;

public class ReliefbildTests
{
    private static AscRaster Raster(int spalten, int zeilen, Func<int, int, float> wert,
                                    double zellgroesse = 1.0)
    {
        var text = new StringBuilder();
        text.Append($"ncols {spalten}\nnrows {zeilen}\nxllcorner 0\nyllcorner 0\n");
        text.Append($"cellsize {zellgroesse.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");
        text.Append("NODATA_value -9999\n");

        for (var y = 0; y < zeilen; y++)
        {
            for (var x = 0; x < spalten; x++)
            {
                if (x > 0) text.Append(' ');
                text.Append(wert(x, y).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            text.Append('\n');
        }

        return AscRaster.Laden(new MemoryStream(Encoding.ASCII.GetBytes(text.ToString())));
    }

    [Fact]
    public void Ein_kleines_Raster_bleibt_unveraendert()
    {
        var raster = Raster(4, 4, (x, y) => x + y);

        var klein = Reliefbild.Verkleinern(raster, 16);

        Assert.Equal(1, klein.Faktor);
        Assert.Equal(4, klein.Spalten);
        Assert.Equal(4, klein.Zeilen);
    }

    [Fact]
    public void Die_Verkleinerung_bildet_den_Blockmittelwert()
    {
        // 4x4, Werte 0..15 zeilenweise. Auf 2x2 verkleinert ist der linke
        // obere Block der Mittelwert aus 0, 1, 4, 5 = 2,5.
        var raster = Raster(4, 4, (x, y) => y * 4 + x);

        var klein = Reliefbild.Verkleinern(raster, 2);

        Assert.Equal(2, klein.Faktor);
        Assert.Equal(2, klein.Spalten);
        Assert.Equal(2.5f, klein[0, 0], precision: 4);
        Assert.Equal(4.5f, klein[1, 0], precision: 4);   // 2,3,6,7
        Assert.Equal(10.5f, klein[0, 1], precision: 4);  // 8,9,12,13
    }

    [Fact]
    public void Die_Verkleinerung_uebergeht_NoData_im_Mittelwert()
    {
        var raster = Raster(2, 2, (x, y) => x == 0 && y == 0 ? -9999f : 10f);

        var klein = Reliefbild.Verkleinern(raster, 1);

        // Drei gueltige Zellen mit 10, eine NoData -> Mittelwert 10.
        Assert.Equal(10f, klein[0, 0], precision: 4);
    }

    [Fact]
    public void Ein_Block_nur_aus_NoData_wird_NaN()
    {
        var raster = Raster(2, 2, (_, _) => -9999f);

        var klein = Reliefbild.Verkleinern(raster, 1);

        Assert.True(float.IsNaN(klein[0, 0]));
    }

    [Fact]
    public void Minimum_und_Maximum_der_Verkleinerung_stammen_aus_den_Mittelwerten()
    {
        var raster = Raster(4, 4, (x, y) => y * 4 + x);

        var klein = Reliefbild.Verkleinern(raster, 2);

        Assert.Equal(2.5f, klein.Min, precision: 4);
        Assert.Equal(12.5f, klein.Max, precision: 4);
    }

    [Fact]
    public void Das_eingefaerbte_Bild_hat_vier_Byte_je_Pixel()
    {
        var klein = Reliefbild.Verkleinern(Raster(8, 8, (x, y) => x + y), 8);

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Gelaende, 1.0);

        Assert.Equal(8 * 8 * 4, bild.Length);
    }

    [Fact]
    public void Alle_Pixel_sind_undurchsichtig()
    {
        var klein = Reliefbild.Verkleinern(Raster(4, 4, (x, y) => x * y), 4);

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Gelaende, 1.0);

        for (var i = 3; i < bild.Length; i += 4)
            Assert.Equal(0xFF, bild[i]);
    }

    [Fact]
    public void Tief_und_hoch_bekommen_bei_Graustufen_dunkel_und_hell()
    {
        // Waagerechter Verlauf, damit die Schummerung nicht dazwischenfunkt.
        var klein = Reliefbild.Verkleinern(Raster(16, 1, (x, _) => x), 16);

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Graustufen, 1.0, schummerung: 0);

        var linksHell = bild[0] + bild[1] + bild[2];
        var rechtsHell = bild[15 * 4] + bild[15 * 4 + 1] + bild[15 * 4 + 2];

        Assert.True(rechtsHell > linksHell,
            $"Hoch sollte heller sein als tief: links={linksHell} rechts={rechtsHell}");
    }

    [Fact]
    public void NoData_wird_als_eigene_Farbe_gekennzeichnet()
    {
        var raster = Raster(2, 1, (x, _) => x == 0 ? -9999f : 100f);
        var klein = Reliefbild.Verkleinern(raster, 2);

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Graustufen, 1.0, schummerung: 0);

        // Erstes Pixel ist NoData und darf nicht schwarz sein, sonst waere
        // es von tiefem Gelaende nicht zu unterscheiden.
        var istSchwarz = bild[0] == 0 && bild[1] == 0 && bild[2] == 0;
        Assert.False(istSchwarz);
        Assert.NotEqual(bild[0], bild[2]);   // farbig, nicht grau
    }

    [Fact]
    public void Eine_ebene_Flaeche_wird_gleichmaessig_beleuchtet()
    {
        var klein = Reliefbild.Verkleinern(Raster(8, 8, (_, _) => 500f), 8);

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Graustufen, 1.0);

        var erstes = (bild[0], bild[1], bild[2]);
        for (var i = 0; i < bild.Length; i += 4)
            Assert.Equal(erstes, (bild[i], bild[i + 1], bild[i + 2]));
    }

    [Fact]
    public void Die_Schummerung_macht_gegenueberliegende_Haenge_verschieden_hell()
    {
        // Ein Rücken: erst ansteigend, dann abfallend. Bei Licht aus
        // Nordwest muss eine Seite heller sein als die andere.
        var raster = Raster(16, 16, (x, _) => x < 8 ? x * 20f : (15 - x) * 20f);
        var klein = Reliefbild.Verkleinern(raster, 16);

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Graustufen, 10.0, schummerung: 1.0);

        int Helligkeit(int x, int y)
        {
            var i = (y * 16 + x) * 4;
            return bild[i] + bild[i + 1] + bild[i + 2];
        }

        Assert.NotEqual(Helligkeit(3, 8), Helligkeit(12, 8));
    }

    [Fact]
    public void Ohne_Schummerung_haengt_die_Farbe_nur_an_der_Hoehe()
    {
        var raster = Raster(16, 16, (x, _) => x < 8 ? x * 20f : (15 - x) * 20f);
        var klein = Reliefbild.Verkleinern(raster, 16);

        var bild = Reliefbild.Einfaerben(klein, Farbskala.Graustufen, 10.0, schummerung: 0);

        int Helligkeit(int x, int y)
        {
            var i = (y * 16 + x) * 4;
            return bild[i] + bild[i + 1] + bild[i + 2];
        }

        // x=3 und x=12 haben dieselbe Hoehe (60 bzw. 60).
        Assert.Equal(Helligkeit(3, 8), Helligkeit(12, 8));
    }

    [Fact]
    public void Alle_Farbskalen_liefern_ein_Bild()
    {
        var klein = Reliefbild.Verkleinern(Raster(8, 8, (x, y) => x + y), 8);

        foreach (var skala in Enum.GetValues<Farbskala>())
        {
            var bild = Reliefbild.Einfaerben(klein, skala, 1.0);
            Assert.Equal(8 * 8 * 4, bild.Length);
        }
    }
}
