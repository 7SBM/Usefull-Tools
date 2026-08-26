using DzAssets.Formats.Katalog;

namespace DzAssets.Tests;

public class ConfigClassIndexTests
{
    [Fact]
    public void Ordnet_einen_Klassennamen_dem_Modellpfad_zu()
    {
        const string text = """
            class CfgVehicles
            {
                class HouseNoDestruct;
                class Land_Wand_A : HouseNoDestruct
                {
                    scope=1;
                    model="DZ\structures\walls\wand_a.p3d";
                };
            };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_Wand_A", index.KlassenFuer(@"DZ\structures\walls\wand_a.p3d"));
    }

    [Fact]
    public void Die_Zuordnung_ignoriert_Gross_und_Kleinschreibung_und_Schraegstriche()
    {
        const string text = """
            class Land_X { model="DZ\a\b.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_X", index.KlassenFuer(@"dz/a/b.p3d"));
    }

    [Fact]
    public void Mehrere_Klassen_koennen_auf_dasselbe_Modell_zeigen()
    {
        const string text = """
            class Land_A { model="DZ\a\b.p3d"; };
            class Land_B { model="DZ\a\b.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        var klassen = index.KlassenFuer(@"DZ\a\b.p3d");
        Assert.Equal(2, klassen.Count);
        Assert.Contains("Land_A", klassen);
        Assert.Contains("Land_B", klassen);
    }

    [Fact]
    public void Dieselbe_Klasse_wird_nicht_doppelt_gefuehrt()
    {
        const string text = """
            class Land_A { model="DZ\a\b.p3d"; };
            class Land_A { model="DZ\a\b.p3d"; };
            """;

        Assert.Single(ConfigClassIndex.AusText(text).KlassenFuer(@"DZ\a\b.p3d"));
    }

    [Fact]
    public void Kommentare_werden_uebergangen()
    {
        const string text = """
            // class Land_Falsch { model="DZ\falsch.p3d"; };
            /* class Land_AuchFalsch { model="DZ\auch_falsch.p3d"; }; */
            class Land_Richtig { model="DZ\richtig.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Empty(index.KlassenFuer(@"DZ\falsch.p3d"));
        Assert.Empty(index.KlassenFuer(@"DZ\auch_falsch.p3d"));
        Assert.Contains("Land_Richtig", index.KlassenFuer(@"DZ\richtig.p3d"));
    }

    [Fact]
    public void Eine_Vorwaertsdeklaration_ohne_Rumpf_stoert_die_Zuordnung_nicht()
    {
        const string text = """
            class Basis;
            class Land_A : Basis { model="DZ\a.p3d"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_A", index.KlassenFuer(@"DZ\a.p3d"));
        Assert.Single(index.KlassenFuer(@"DZ\a.p3d"));
    }

    [Fact]
    public void Der_innerste_Klassenname_gewinnt_bei_Verschachtelung()
    {
        const string text = """
            class CfgVehicles
            {
                class Land_Aussen
                {
                    model="DZ\aussen.p3d";
                    class Land_Innen
                    {
                        model="DZ\innen.p3d";
                    };
                };
            };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_Aussen", index.KlassenFuer(@"DZ\aussen.p3d"));
        Assert.Contains("Land_Innen", index.KlassenFuer(@"DZ\innen.p3d"));
    }

    [Fact]
    public void Ein_Modellpfad_ohne_Endung_wird_ergaenzt()
    {
        const string text = """
            class Land_A { model="DZ\a\ohne_endung"; };
            """;

        var index = ConfigClassIndex.AusText(text);

        Assert.Contains("Land_A", index.KlassenFuer(@"DZ\a\ohne_endung.p3d"));
    }

    [Fact]
    public void Ein_unbekanntes_Modell_liefert_eine_leere_Liste_statt_null()
    {
        var index = ConfigClassIndex.AusText("class Leer {};");

        Assert.Empty(index.KlassenFuer(@"DZ\gibt-es-nicht.p3d"));
    }

    [Fact]
    public void Ein_leerer_Text_ergibt_einen_leeren_Index()
    {
        Assert.Equal(0, ConfigClassIndex.AusText(string.Empty).AnzahlKlassen);
    }

    [PDriveFact]
    public void Liest_die_echte_config_cpp_der_Pflanzen()
    {
        // DZ\plants\config.cpp enthaelt genau zwei model=-Eintraege
        // (am 2026-08-26 nachgezaehlt). Die Pflanzen werden ganz
        // ueberwiegend ueber Makros beschrieben, nicht ueber model=.
        var index = ConfigClassIndex.Erstellen([Path.Combine(TestAssets.PDrive!, "DZ", "plants")]);

        Assert.Equal(2, index.AnzahlKlassen);
        Assert.NotEmpty(index.KlassenFuer(@"DZ\plants\tree\t_PiceaAbies_2s_xmas.p3d"));
        Assert.NotEmpty(index.KlassenFuer(@"DZ\plants\tree\t_PiceaAbies_2s_green_xmas.p3d"));
    }

    [PDriveFact]
    public void Liest_den_gesamten_Bestand_ohne_Ausnahme()
    {
        // Ueber alle config.cpp unterhalb von DZ standen am 2026-08-26
        // 496 model=-Eintraege; nach Zusammenfassung gleicher Pfade
        // bleiben deutlich mehr als hundert Modelle uebrig.
        var index = ConfigClassIndex.Erstellen([Path.Combine(TestAssets.PDrive!, "DZ")]);

        Assert.True(index.AnzahlModelle > 100,
            $"Nur {index.AnzahlModelle} Modelle mit Klassennamen gefunden");
    }
}
