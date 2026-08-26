using DzAssets.Formats.Models;

namespace DzAssets.Tests;

/// <summary>
/// Zwei Befunde aus dem Betrieb: Texturen mit "P:\"-Praefix wurden nicht
/// gefunden, und die Proxy-Platzhalter standen mitten im Modell.
/// </summary>
public class TexturpfadMitLaufwerkTests
{
    [PDriveFact]
    public void Ein_Pfad_mit_P_Praefix_wird_relativ_zur_Wurzel_aufgeloest()
    {
        // Die Modelle des Anwenders verweisen auf "P:\...", das
        // Arma-Arbeitslaufwerk. Eingebunden ist es nicht; H:\P_Drive ist
        // sein Spiegel. Ohne Behandlung bliebe das Modell grau.
        var vorhanden = TestAssets.ErsteDatei("structures", "*_co.paa");
        var relativ = Path.GetRelativePath(TestAssets.PDrive!, vorhanden);

        var aufloeser = new TextureResolver(TestAssets.PDrive!);

        var ergebnis = aufloeser.ZuAbsolut(@"P:\" + relativ);

        Assert.NotNull(ergebnis);
        Assert.Equal(vorhanden, ergebnis, StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(ergebnis));
    }

    [PDriveFact]
    public void Der_Laufwerksbuchstabe_wird_unabhaengig_von_der_Schreibweise_abgestreift()
    {
        // Eine Datei nehmen, die es wirklich gibt, und ihr ein "P:\"
        // voranstellen — so wie es in den Modellen des Anwenders steht.
        var vorhanden = TestAssets.ErsteDatei("structures", "*_co.paa");
        var relativ = Path.GetRelativePath(TestAssets.PDrive!, vorhanden);

        var aufloeser = new TextureResolver(TestAssets.PDrive!);

        var klein = aufloeser.ZuAbsolut(@"p:\" + relativ);
        var gross = aufloeser.ZuAbsolut(@"P:\" + relativ);

        Assert.Equal(vorhanden, klein, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(klein, gross, StringComparer.OrdinalIgnoreCase);
    }

    [PDriveFact]
    public void Ein_nirgends_auffindbarer_absoluter_Pfad_bleibt_unveraendert()
    {
        // Wichtig fuer die Fehlermeldung: gezeigt wird, was im Modell
        // steht, nicht ein hier erfundener Ort.
        var aufloeser = new TextureResolver(TestAssets.PDrive!);

        Assert.Equal(@"P:\gibt-es-nirgends\x.paa",
                     aufloeser.ZuAbsolut(@"P:\gibt-es-nirgends\x.paa"));
    }

    [PDriveFact]
    public void Eine_vorhandene_absolute_Datei_bleibt_unangetastet()
    {
        var vorhanden = TestAssets.ErsteDatei("structures", "*_co.paa");
        var aufloeser = new TextureResolver(TestAssets.PDrive!);

        Assert.Equal(vorhanden, aufloeser.ZuAbsolut(vorhanden));
    }

    [Fact]
    public void Ein_absoluter_Pfad_ohne_Wurzeln_bleibt_wie_er_ist()
    {
        var aufloeser = new TextureResolver([]);

        Assert.Equal(@"Q:\gibt-es-nicht\x.paa", aufloeser.ZuAbsolut(@"Q:\gibt-es-nicht\x.paa"));
    }
}

public class ProxyErkennungTests
{
    [PDriveFact]
    public void Ein_Modell_mit_Proxys_trennt_sie_von_der_sichtbaren_Geometrie()
    {
        // Unter DZ gibt es reichlich Modelle mit Proxys; das erste
        // passende genuegt fuer die Pruefung.
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ", "structures");

        LodGeometry? mitProxy = null;
        foreach (var datei in Directory.EnumerateFiles(wurzel, "*.p3d", SearchOption.AllDirectories).Take(300))
        {
            try
            {
                var lod = P3dModelReader.Read(datei).FeinsterSichtbarerLod;
                if (lod is not null && lod.HatProxies) { mitProxy = lod; break; }
            }
            catch (Exception)
            {
                // Ein unlesbares Modell ueberspringen.
            }
        }

        if (mitProxy is null) return;   // keines gefunden: nichts zu pruefen

        Assert.True(mitProxy.TriangleCountOhneProxy < mitProxy.TriangleCount,
            "Bei einem Modell mit Proxys muss die Zahl ohne sie kleiner sein.");
        Assert.True(mitProxy.TriangleCountOhneProxy > 0,
            "Das sichtbare Objekt darf nicht vollstaendig als Proxy gelten.");
    }

    [PDriveFact]
    public void Ohne_Proxys_stimmen_beide_Dreieckszahlen_ueberein()
    {
        var lod = P3dModelReader
            .Read(TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"))
            .FeinsterSichtbarerLod!;

        if (lod.HatProxies) return;

        Assert.Equal(lod.TriangleCount, lod.TriangleCountOhneProxy);
    }

    [Fact]
    public void Ein_Abschnitt_ist_ohne_ausdrueckliche_Angabe_kein_Proxy()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        Assert.False(abschnitt.IstProxy);
    }

    [Fact]
    public void Die_Zaehlung_ohne_Proxy_laesst_Proxy_Abschnitte_aus()
    {
        var lod = new LodGeometry
        {
            Resolution = 1, Name = "Stufe 1", IstSichtbar = true,
            Positions = [new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0)],
            Normals = [new Vec3(0, 1, 0), new Vec3(0, 1, 0), new Vec3(0, 1, 0)],
            Uvs = [new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1)],
            Sections =
            [
                new MeshSection { Indices = [0, 1, 2] },
                new MeshSection { Indices = [0, 1, 2], IstProxy = true },
            ],
        };

        Assert.Equal(2, lod.TriangleCount);
        Assert.Equal(1, lod.TriangleCountOhneProxy);
        Assert.True(lod.HatProxies);
    }
}
