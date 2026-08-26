using DzAssets.Formats.Models;

namespace DzAssets.Tests;

public class RvmatMaterialTests
{
    [Fact]
    public void Findet_die_Diffusetextur_anhand_der_co_Endung()
    {
        const string text = """
            ambient[]={1,1,1,1};
            class Stage1
            {
                texture="DZ\structures\data\wand_nohq.paa";
            };
            class Stage2
            {
                texture="DZ\structures\data\wand_co.paa";
            };
            """;

        var material = RvmatMaterial.Parse(text);

        Assert.Equal(@"DZ\structures\data\wand_co.paa", material.DiffusePfad);
        Assert.Equal(2, material.StageTextures.Count);
    }

    [Fact]
    public void Ohne_co_Textur_gewinnt_die_erste_Stage()
    {
        const string text = """
            class Stage1 { texture="DZ\a\b_nohq.paa"; };
            class Stage2 { texture="DZ\a\b_as.paa"; };
            """;

        var material = RvmatMaterial.Parse(text);

        Assert.Equal(@"DZ\a\b_nohq.paa", material.DiffusePfad);
    }

    [Fact]
    public void Ein_binarisiertes_rvmat_liefert_keine_Textur()
    {
        var material = RvmatMaterial.Parse("raP\0\0\0irgendwas");

        Assert.Null(material.DiffusePfad);
        Assert.Empty(material.StageTextures);
    }

    [Fact]
    public void Prozedurale_Texturen_werden_uebergangen()
    {
        const string text = """
            class Stage1 { texture="#(argb,8,8,3)color(0.5,0.5,0.5,1)"; };
            class Stage2 { texture="DZ\a\b_co.paa"; };
            """;

        var material = RvmatMaterial.Parse(text);

        Assert.Equal(@"DZ\a\b_co.paa", material.DiffusePfad);
        Assert.DoesNotContain(material.StageTextures, t => t.StartsWith('#'));
    }

    [Fact]
    public void Ein_leerer_Text_ergibt_ein_leeres_Material()
    {
        var material = RvmatMaterial.Parse(string.Empty);

        Assert.Null(material.DiffusePfad);
        Assert.Empty(material.StageTextures);
    }

    [Fact]
    public void Die_Suche_nach_co_ignoriert_Gross_und_Kleinschreibung()
    {
        const string text = """
            class Stage1 { TEXTURE="DZ\a\b_NOHQ.paa"; };
            class Stage2 { texture="DZ\a\b_CO.PAA"; };
            """;

        var material = RvmatMaterial.Parse(text);

        Assert.Equal(@"DZ\a\b_CO.PAA", material.DiffusePfad);
    }
}

public class TextureResolverTests
{
    [PDriveFact]
    public void Wandelt_einen_Assetpfad_in_einen_absoluten_Pfad()
    {
        var aufloeser = new TextureResolver(TestAssets.PDrive!);

        var ergebnis = aufloeser.ZuAbsolut(@"DZ\structures\data\irgendwas_co.paa");

        Assert.NotNull(ergebnis);
        Assert.StartsWith(TestAssets.PDrive!, ergebnis, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"irgendwas_co.paa", ergebnis);
    }

    [Fact]
    public void Ein_fuehrender_Schraegstrich_stoert_die_Umwandlung_nicht()
    {
        var aufloeser = new TextureResolver(@"H:\P_Drive");

        Assert.Equal(@"H:\P_Drive\DZ\a\b.paa", aufloeser.ZuAbsolut(@"\DZ\a\b.paa"));
        Assert.Equal(@"H:\P_Drive\DZ\a\b.paa", aufloeser.ZuAbsolut(@"DZ/a/b.paa"));
    }

    [Fact]
    public void Ein_bereits_absoluter_Pfad_bleibt_unveraendert()
    {
        var aufloeser = new TextureResolver(@"H:\P_Drive");

        Assert.Equal(@"D:\woanders\x.paa", aufloeser.ZuAbsolut(@"D:\woanders\x.paa"));
    }

    [PDriveFact]
    public void Findet_fuer_ein_echtes_Modell_mindestens_eine_vorhandene_Textur()
    {
        var modell = P3dModelReader.Read(
            TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"));
        var aufloeser = new TextureResolver(TestAssets.PDrive!);
        var lod = modell.FeinsterSichtbarerLod!;

        var gefunden = lod.Sections
            .Select(aufloeser.DiffuseFuer)
            .Where(p => p is not null)
            .ToList();

        Assert.NotEmpty(gefunden);
        Assert.All(gefunden, p => Assert.True(File.Exists(p), $"Nicht vorhanden: {p}"));
    }

    [Fact]
    public void Ein_leerer_Abschnitt_liefert_null_statt_einer_Ausnahme()
    {
        var aufloeser = new TextureResolver(@"C:\gibt-es-nicht");
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        Assert.Null(aufloeser.DiffuseFuer(abschnitt));
    }

    [PDriveFact]
    public void Die_meisten_Abschnitte_echter_Modelle_bekommen_eine_Textur()
    {
        // Die entscheidende Frage fuer die Vorschau: bleibt das Modell grau?
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ", "structures");
        var dateien = Directory.EnumerateFiles(wurzel, "*.p3d", SearchOption.AllDirectories)
            .Take(60)
            .ToList();

        var aufloeser = new TextureResolver(TestAssets.PDrive!);
        var mitTextur = 0;
        var gesamt = 0;

        foreach (var datei in dateien)
        {
            var lod = P3dModelReader.Read(datei).FeinsterSichtbarerLod;
            if (lod is null) continue;

            foreach (var abschnitt in lod.Sections)
            {
                gesamt++;
                if (aufloeser.DiffuseFuer(abschnitt) is not null) mitTextur++;
            }
        }

        Assert.True(gesamt > 0, "Keine Abschnitte gefunden.");
        Assert.True(mitTextur * 2 > gesamt,
            $"Nur {mitTextur} von {gesamt} Abschnitten bekamen eine Textur.");
    }
}
