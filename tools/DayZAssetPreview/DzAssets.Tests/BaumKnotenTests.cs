using DzAssets.Formats.Katalog;
using DzAssets.Preview.Module.AssetVorschau;

namespace DzAssets.Tests;

public class BaumKnotenTests
{
    private static AssetEintrag Eintrag(string assetPfad) => new(
        AbsoluterPfad: @"H:\P_Drive\" + assetPfad,
        AssetPfad: assetPfad,
        Name: Path.GetFileNameWithoutExtension(assetPfad),
        Ordner: Path.GetDirectoryName(assetPfad) ?? string.Empty,
        Groesse: 100,
        GeaendertUtc: DateTime.UnixEpoch);

    [Fact]
    public void Baut_aus_flachen_Pfaden_einen_Ordnerbaum()
    {
        var baum = BaumKnoten.BaumBauen([
            Eintrag(@"DZ\structures\haus\a.p3d"),
            Eintrag(@"DZ\structures\haus\b.p3d"),
            Eintrag(@"DZ\plants\baum\c.p3d"),
        ]);

        var dz = Assert.Single(baum);
        Assert.Equal("DZ", dz.Beschriftung);
        Assert.Equal(2, dz.Kinder.Count);           // structures, plants
    }

    [Fact]
    public void Blaetter_tragen_den_Assetpfad_Ordner_nicht()
    {
        var baum = BaumKnoten.BaumBauen([Eintrag(@"DZ\a\b.p3d")]);

        var dz = baum[0];
        var a = dz.Kinder[0];
        var blatt = a.Kinder[0];

        Assert.True(dz.IstOrdner);
        Assert.Null(dz.AssetPfad);
        Assert.False(blatt.IstOrdner);
        Assert.Equal(@"DZ\a\b.p3d", blatt.AssetPfad);
        Assert.Equal("b", blatt.Beschriftung);
        Assert.Equal(@"H:\P_Drive\DZ\a\b.p3d", blatt.AbsoluterPfad);
    }

    [Fact]
    public void Ordner_stehen_vor_Dateien_und_sind_alphabetisch_sortiert()
    {
        var baum = BaumKnoten.BaumBauen([
            Eintrag(@"DZ\zebra.p3d"),
            Eintrag(@"DZ\alpha.p3d"),
            Eintrag(@"DZ\unterordner\x.p3d"),
        ]);

        var kinder = baum[0].Kinder;
        Assert.Equal("unterordner", kinder[0].Beschriftung);
        Assert.True(kinder[0].IstOrdner);
        Assert.Equal("alpha", kinder[1].Beschriftung);
        Assert.Equal("zebra", kinder[2].Beschriftung);
    }

    [Fact]
    public void Mehrere_Wurzeln_bleiben_nebeneinander_bestehen()
    {
        var baum = BaumKnoten.BaumBauen([
            Eintrag(@"DZ\a.p3d"),
            Eintrag(@"7SBM_World\b.p3d"),
        ]);

        Assert.Equal(2, baum.Count);
        Assert.Contains(baum, k => k.Beschriftung == "DZ");
        Assert.Contains(baum, k => k.Beschriftung == "7SBM_World");
    }

    [Fact]
    public void Ordner_und_Datei_haben_verschiedene_Symbole()
    {
        var baum = BaumKnoten.BaumBauen([Eintrag(@"DZ\a.p3d")]);

        var ordner = baum[0];
        var blatt = ordner.Kinder[0];

        Assert.NotEqual(ordner.Symbol, blatt.Symbol);
        Assert.NotEmpty(ordner.Symbol);
        Assert.NotEmpty(blatt.Symbol);
    }

    [Fact]
    public void Eine_leere_Liste_ergibt_einen_leeren_Baum()
    {
        Assert.Empty(BaumKnoten.BaumBauen([]));
    }

    [Fact]
    public void Zehntausend_Eintraege_werden_in_unter_zwei_Sekunden_verbaut()
    {
        var eintraege = Enumerable.Range(0, 10_000)
            .Select(i => Eintrag($@"DZ\gruppe{i % 50}\unter{i % 7}\modell{i}.p3d"))
            .ToList();

        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var baum = BaumKnoten.BaumBauen(eintraege);
        uhr.Stop();

        Assert.NotEmpty(baum);
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(2), $"Dauer {uhr.Elapsed}");
    }

    [Fact]
    public void Das_Aufklappen_wird_gemeldet()
    {
        var knoten = BaumKnoten.BaumBauen([Eintrag(@"DZ\a.p3d")])[0];
        var gemeldet = new List<string?>();
        knoten.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName);

        knoten.IstAufgeklappt = true;

        Assert.Contains(nameof(BaumKnoten.IstAufgeklappt), gemeldet);
    }
}
