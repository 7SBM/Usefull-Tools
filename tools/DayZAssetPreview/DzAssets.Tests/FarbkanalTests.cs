using DzAssets.Formats.Models;
using DzAssets.Formats.Textures;

namespace DzAssets.Tests;

/// <summary>
/// Sichert die Reihenfolge der Farbkanaele ueber die gesamte Kette ab:
/// PAA lesen, LZO entpacken, DXT dekodieren, nach BGRA sortieren.
///
/// Eine Vertauschung von Rot und Blau faellt bei grauen oder weissen
/// Texturen nicht auf — deshalb wird hier an Motiven geprueft, deren
/// Farbstich bekannt ist.
/// </summary>
public class FarbkanalTests
{
    private static (int R, int G, int B, int Undurchsichtig) Mittelwert(string datei)
    {
        var paa = PaaImage.Load(datei);

        long b = 0, g = 0, r = 0, zahl = 0;
        for (var i = 0; i < paa.Bgra.Length; i += 4)
        {
            if (paa.Bgra[i + 3] < 128) continue;
            b += paa.Bgra[i];
            g += paa.Bgra[i + 1];
            r += paa.Bgra[i + 2];
            zahl++;
        }

        return zahl == 0
            ? (0, 0, 0, 0)
            : ((int)(r / zahl), (int)(g / zahl), (int)(b / zahl), (int)zahl);
    }

    [PDriveFact]
    public void Birkenrinde_ist_warmes_Weiss_und_nicht_blaeulich()
    {
        // Birkenrinde ist hell und warm. Waeren Rot und Blau vertauscht,
        // ergaebe sich ein kaltes Blaugrau.
        var datei = TestAssets.Dz(@"plants\tree\data\t_betula_pendula_bark_01_co.paa");
        Assert.True(File.Exists(datei), $"Testtextur fehlt: {datei}");

        var (r, g, b, undurchsichtig) = Mittelwert(datei);

        Assert.True(undurchsichtig > 1000, "Die Rinde sollte praktisch undurchsichtig sein.");
        Assert.True(r > 120 && g > 120 && b > 100, $"Zu dunkel: R={r} G={g} B={b}");
        Assert.True(r > b, $"Rinde ist nicht warm — R={r} muss groesser als B={b} sein. "
                           + "Sind Rot und Blau vertauscht?");
    }

    [PDriveFact]
    public void Birkenlaub_ist_ueberwiegend_durchsichtig_und_warm_getoent()
    {
        // Eine Blattkarte besteht ganz ueberwiegend aus leerem Raum.
        var datei = TestAssets.Dz(@"plants\tree\data\t_betulapendula_3s_leaves_co.paa");
        Assert.True(File.Exists(datei), $"Testtextur fehlt: {datei}");

        var paa = PaaImage.Load(datei);
        Assert.True(paa.HatTransparenz, "Eine Blattkarte muss durchsichtige Bereiche haben.");

        var (r, g, b, undurchsichtig) = Mittelwert(datei);
        var gesamt = paa.Width * paa.Height;

        Assert.True(undurchsichtig < gesamt / 2,
            $"{undurchsichtig} von {gesamt} Pixeln undurchsichtig — fuer eine Blattkarte zu viel.");
        Assert.True(r > b, $"Herbstlaub ist warm — R={r} muss groesser als B={b} sein. "
                           + "Sind Rot und Blau vertauscht?");
    }

    [PDriveFact]
    public void Die_Textur_eines_Modells_wird_ueber_das_Material_gefunden()
    {
        // Bei der Birke steht die Textur direkt am Abschnitt; der Test
        // sichert, dass die Aufloesung den vollstaendigen Pfad liefert.
        var modell = P3dModelReader.Read(TestAssets.Dz(@"plants\tree\t_betulapendula_1f.p3d"));
        var lod = modell.FeinsterSichtbarerLod!;
        var aufloeser = new TextureResolver(TestAssets.PDrive!);

        var gefunden = lod.Sections.Select(aufloeser.DiffuseFuer).Where(p => p is not null).ToList();

        Assert.Equal(lod.Sections.Length, gefunden.Count);
        Assert.All(gefunden, p => Assert.EndsWith("_co.paa", p, StringComparison.OrdinalIgnoreCase));
    }
}
