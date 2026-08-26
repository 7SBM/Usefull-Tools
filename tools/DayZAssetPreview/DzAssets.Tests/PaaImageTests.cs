using DzAssets.Formats.Textures;

namespace DzAssets.Tests;

public class PaaImageTests
{
    [PDriveFact]
    public void Laedt_eine_echte_DXT_Textur_mit_plausibler_Groesse()
    {
        var pfad = TestAssets.ErsteDatei("structures", "*_co.paa");

        var bild = PaaImage.Load(pfad);

        Assert.True(bild.Width > 0 && bild.Width <= 8192, $"Breite {bild.Width}");
        Assert.True(bild.Height > 0 && bild.Height <= 8192, $"Hoehe {bild.Height}");
        Assert.Equal(bild.Width * bild.Height * 4, bild.Bgra.Length);
    }

    [PDriveFact]
    public void Die_groesste_Mipmap_wird_gelesen_nicht_die_kleinste()
    {
        var pfad = TestAssets.ErsteDatei("structures", "*_co.paa");

        var bild = PaaImage.Load(pfad);

        // DayZ-Diffusetexturen sind praktisch nie kleiner als 64 Pixel.
        Assert.True(bild.Width >= 64, $"Breite {bild.Width} deutet auf eine kleine Mipmap hin");
    }

    [PDriveFact]
    public void Erkennt_das_Kompressionsformat()
    {
        var pfad = TestAssets.ErsteDatei("structures", "*_co.paa");

        var bild = PaaImage.Load(pfad);

        Assert.Contains(bild.Format, new[] { PaaFormat.Dxt1, PaaFormat.Dxt5, PaaFormat.Argb8888 });
    }

    [PDriveFact]
    public void Das_Bild_ist_nicht_durchgehend_schwarz()
    {
        // Ein rein schwarzes Ergebnis waere das typische Zeichen dafuer,
        // dass die Entpackung stillschweigend fehlgeschlagen ist.
        var pfad = TestAssets.ErsteDatei("structures", "*_co.paa");

        var bild = PaaImage.Load(pfad);

        var summe = 0L;
        for (var i = 0; i < bild.Bgra.Length; i += 4)
            summe += bild.Bgra[i] + bild.Bgra[i + 1] + bild.Bgra[i + 2];

        Assert.True(summe > 0, "Alle Farbwerte sind 0 — die Entpackung hat nicht funktioniert.");
    }

    [PDriveFact]
    public void Fuenfzig_echte_Texturen_laden_ohne_Ausnahme()
    {
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ", "structures");
        var dateien = Directory.EnumerateFiles(wurzel, "*.paa", SearchOption.AllDirectories)
            .Take(50)
            .ToList();

        Assert.NotEmpty(dateien);

        var fehler = new List<string>();
        foreach (var datei in dateien)
        {
            try
            {
                var bild = PaaImage.Load(datei);
                Assert.Equal(bild.Width * bild.Height * 4, bild.Bgra.Length);
            }
            catch (Exception ausnahme)
            {
                fehler.Add($"{Path.GetFileName(datei)}: {ausnahme.GetType().Name} {ausnahme.Message}");
            }
        }

        Assert.True(fehler.Count == 0, string.Join(Environment.NewLine, fehler.Take(10)));
    }

    [Fact]
    public void AlphaQuantisieren_macht_aus_Halbtransparenz_null_oder_voll()
    {
        var bild = PaaImage.FuerTest(2, 1, PaaFormat.Dxt5,
        [
            0, 0, 0, 100,   // unter der Schwelle
            0, 0, 0, 200,   // ueber der Schwelle
        ]);

        bild.AlphaQuantisieren(128);

        Assert.Equal(0, bild.Bgra[3]);
        Assert.Equal(255, bild.Bgra[7]);
    }

    [Fact]
    public void Eine_unlesbare_Datei_wirft_eine_aussagekraeftige_Ausnahme()
    {
        using var strom = new MemoryStream([1, 2, 3]);

        var fehler = Assert.Throws<InvalidDataException>(() => PaaImage.Load(strom));

        Assert.Contains("PAA", fehler.Message);
    }
}
