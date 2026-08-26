using System.Text;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Hoehen;
using DzAssets.Preview.Gemeinsam;
using DzAssets.Preview.Module.Hoehenkarte;

namespace DzAssets.Tests;

public class GelaendeNetzTests
{
    private static Reliefbild.Verkleinert Raster(int spalten, int zeilen,
                                                 Func<int, int, float> wert,
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

        var roh = AscRaster.Laden(new MemoryStream(Encoding.ASCII.GetBytes(text.ToString())));
        return Reliefbild.Verkleinern(roh, Math.Max(spalten, zeilen));
    }

    [Fact]
    public void Das_Netz_hat_einen_Punkt_je_Rasterzelle()
    {
        var netz = GelaendeNetz.Bauen(Raster(8, 6, (x, y) => x + y), 1.0);

        Assert.Equal(8 * 6, netz.Positions.Count);
        Assert.Equal(8 * 6, netz.TextureCoordinates.Count);
    }

    [Fact]
    public void Die_Dreiecksanzahl_entspricht_zwei_je_Zelle()
    {
        var netz = GelaendeNetz.Bauen(Raster(8, 6, (x, y) => x + y), 1.0);

        Assert.Equal((8 - 1) * (6 - 1) * 2 * 3, netz.TriangleIndices.Count);
    }

    [Fact]
    public void Das_Netz_ist_um_den_Ursprung_zentriert()
    {
        // 5 Zellen a 10 m -> 40 m Spannweite, also -20 bis +20.
        var netz = GelaendeNetz.Bauen(Raster(5, 5, (_, _) => 0f, zellgroesse: 10), 10.0);

        var minX = netz.Positions.Min(p => p.X);
        var maxX = netz.Positions.Max(p => p.X);

        Assert.Equal(-20, minX, precision: 4);
        Assert.Equal(20, maxX, precision: 4);
    }

    [Fact]
    public void Die_Hoehe_wird_ab_dem_Minimum_gerechnet()
    {
        // Werte 100..107; der tiefste Punkt liegt auf 0.
        var netz = GelaendeNetz.Bauen(Raster(8, 1, (x, _) => 100 + x), 1.0);

        Assert.Equal(0, netz.Positions.Min(p => p.Y), precision: 4);
        Assert.Equal(7, netz.Positions.Max(p => p.Y), precision: 4);
    }

    [Fact]
    public void Die_Ueberhoehung_streckt_nur_die_Hoehe()
    {
        var raster = Raster(8, 1, (x, _) => 100 + x);

        var einfach = GelaendeNetz.Bauen(raster, 1.0, ueberhoehung: 1.0);
        var dreifach = GelaendeNetz.Bauen(raster, 1.0, ueberhoehung: 3.0);

        Assert.Equal(einfach.Positions.Max(p => p.Y) * 3,
                     dreifach.Positions.Max(p => p.Y), precision: 4);
        Assert.Equal(einfach.Positions.Max(p => p.X),
                     dreifach.Positions.Max(p => p.X), precision: 4);
    }

    [Fact]
    public void Luecken_werden_auf_die_Mindesthoehe_gesetzt_und_nicht_NaN()
    {
        // Ein NaN im Netz laesst WPF die ganze Geometrie verwerfen.
        var netz = GelaendeNetz.Bauen(
            Raster(4, 4, (x, y) => x == 0 && y == 0 ? -9999f : 50f), 1.0);

        Assert.All(netz.Positions, p =>
        {
            Assert.False(double.IsNaN(p.X));
            Assert.False(double.IsNaN(p.Y));
            Assert.False(double.IsNaN(p.Z));
        });
    }

    [Fact]
    public void Die_Texturkoordinaten_laufen_von_null_bis_eins()
    {
        var netz = GelaendeNetz.Bauen(Raster(8, 6, (x, y) => x + y), 1.0);

        Assert.Equal(0.0, netz.TextureCoordinates.Min(p => p.X), precision: 6);
        Assert.Equal(1.0, netz.TextureCoordinates.Max(p => p.X), precision: 6);
        Assert.Equal(0.0, netz.TextureCoordinates.Min(p => p.Y), precision: 6);
        Assert.Equal(1.0, netz.TextureCoordinates.Max(p => p.Y), precision: 6);
    }

    [Fact]
    public void Jeder_Index_zeigt_in_die_Punktliste()
    {
        var netz = GelaendeNetz.Bauen(Raster(16, 16, (x, y) => x * y % 7), 1.0);

        Assert.All(netz.TriangleIndices, i => Assert.InRange(i, 0, netz.Positions.Count - 1));
    }
}

public class OrbitKameraTests
{
    private static OrbitKamera Neu() => new(new PerspectiveCamera
    {
        FieldOfView = 45,
        Position = new Point3D(0, 0, 10),
        LookDirection = new Vector3D(0, 0, -1),
        UpDirection = new Vector3D(0, 1, 0),
    });

    [Fact]
    public void Die_Kamera_blickt_immer_auf_den_Zielpunkt()
    {
        var kamera = Neu();
        kamera.Zielpunkt = new Point3D(5, 2, -3);
        kamera.Abstand = 20;
        kamera.Anwenden();

        var blickZiel = kamera.Kamera.Position + kamera.Kamera.LookDirection;

        Assert.Equal(kamera.Zielpunkt.X, blickZiel.X, precision: 4);
        Assert.Equal(kamera.Zielpunkt.Y, blickZiel.Y, precision: 4);
        Assert.Equal(kamera.Zielpunkt.Z, blickZiel.Z, precision: 4);
    }

    [Fact]
    public void Der_Abstand_zum_Zielpunkt_stimmt()
    {
        var kamera = Neu();
        kamera.Abstand = 42;
        kamera.DrehungUmY = 1.1;
        kamera.DrehungUmX = 0.3;
        kamera.Anwenden();

        var abstand = (kamera.Kamera.Position - kamera.Zielpunkt).Length;

        Assert.Equal(42, abstand, precision: 3);
    }

    [Fact]
    public void Die_Neigung_wird_vor_dem_Ueberschlagen_begrenzt()
    {
        var kamera = Neu();
        kamera.DrehungUmX = 99;
        kamera.Anwenden();

        Assert.InRange(kamera.DrehungUmX, -1.5, 1.5);
    }

    [Fact]
    public void Einrahmen_setzt_den_Zielpunkt_in_die_Mitte()
    {
        var kamera = Neu();

        kamera.Einrahmen(new Point3D(0, 0, 0), new Point3D(10, 4, 6));

        Assert.Equal(5, kamera.Zielpunkt.X, precision: 4);
        Assert.Equal(2, kamera.Zielpunkt.Y, precision: 4);
        Assert.Equal(3, kamera.Zielpunkt.Z, precision: 4);
    }

    [Fact]
    public void Einrahmen_rechnet_mit_der_Raumdiagonale()
    {
        var kamera = Neu();

        // Breit und flach: die laengste Kante allein waere zu wenig.
        kamera.Einrahmen(new Point3D(0, 0, 0), new Point3D(100, 1, 100), zugabe: 1.0);

        var diagonale = Math.Sqrt(100 * 100 + 1 + 100 * 100);
        Assert.Equal(diagonale / 2, kamera.Abstand, precision: 2);
    }

    [Fact]
    public void Das_Rad_veraendert_den_Abstand_in_beide_Richtungen()
    {
        var kamera = Neu();
        kamera.Abstand = 100;
        kamera.Anwenden();

        kamera.Rad(120);
        var naeher = kamera.Abstand;
        Assert.True(naeher < 100);

        kamera.Rad(-120);
        Assert.True(kamera.Abstand > naeher);
    }

    [Fact]
    public void Der_Abstand_bleibt_in_den_Grenzen()
    {
        var kamera = Neu();
        kamera.KleinsterAbstand = 2;
        kamera.GroessterAbstand = 50;

        kamera.Abstand = 1000;
        kamera.Anwenden();
        Assert.Equal(50, kamera.Abstand);

        kamera.Abstand = 0.001;
        kamera.Anwenden();
        Assert.Equal(2, kamera.Abstand);
    }
}
