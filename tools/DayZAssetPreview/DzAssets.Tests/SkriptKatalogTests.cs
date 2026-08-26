using DzAssets.Formats.Skripte;

namespace DzAssets.Tests;

public class SkriptBeschreibungTests
{
    /// <summary>Drei Anfuehrungszeichen, wie sie Python als Docstring nutzt.</summary>
    private const string D = "\"\"\"";

    [Fact]
    public void Ein_Python_Docstring_wird_als_Beschreibung_erkannt()
    {
        // Nachgebaut nach generate_mapgroupproto.py aus DayZ_Helper_Scripte.
        var text = string.Join('\n',
            "#!/usr/bin/env python3",
            D,
            "generate_mapgroupproto.py  -  Brienz Assets MapGroupProto Generator",
            "",
            "Scans all DayZ building mods und erzeugt eine mapgroupproto.xml.",
            D,
            "",
            "import os");

        var beschreibung = SkriptEintrag.BeschreibungAusText(text, SkriptArt.Python);

        Assert.Contains("MapGroupProto Generator", beschreibung);
        Assert.Contains("mapgroupproto.xml", beschreibung);
        Assert.DoesNotContain("import os", beschreibung);
    }

    [Fact]
    public void Ein_einzeiliger_Docstring_wird_erkannt()
    {
        var text = "\"\"\"Kurze Erklaerung.\"\"\"\nimport os\n";

        var beschreibung = SkriptEintrag.BeschreibungAusText(text, SkriptArt.Python);

        Assert.Equal("Kurze Erklaerung.", beschreibung);
    }

    [Fact]
    public void Ohne_Docstring_werden_fuehrende_Rautenzeilen_genommen()
    {
        const string text = "# Macht dies und das\n# ueber zwei Zeilen\n\nimport os\n";

        var beschreibung = SkriptEintrag.BeschreibungAusText(text, SkriptArt.Python);

        Assert.Contains("Macht dies und das", beschreibung);
        Assert.Contains("ueber zwei Zeilen", beschreibung);
    }

    [Fact]
    public void PowerShell_Kopfkommentare_werden_erkannt_und_Zierlinien_entfernt()
    {
        const string text = """
            # ============================================================
            #  P:\ Backup Script — Spiegel ausgewaehlter Ordner nach H:\P_Drive
            # ============================================================

            $SRC_ROOT = "P:\"
            """;

        var beschreibung = SkriptEintrag.BeschreibungAusText(text, SkriptArt.PowerShell);

        Assert.Contains("Backup Script", beschreibung);
        Assert.DoesNotContain("====", beschreibung);
        Assert.DoesNotContain("SRC_ROOT", beschreibung);
    }

    [Fact]
    public void Batch_Kommentare_mit_REM_und_Doppelpunkt_werden_erkannt()
    {
        const string text = ":: Kopiert die Mod nach P\nREM zweite Zeile\n\necho los\n";

        var beschreibung = SkriptEintrag.BeschreibungAusText(text, SkriptArt.Batch);

        Assert.Contains("Kopiert die Mod", beschreibung);
        Assert.Contains("zweite Zeile", beschreibung);
        Assert.DoesNotContain("echo", beschreibung);
    }

    [Fact]
    public void Ein_Skript_ohne_Kopfkommentar_ergibt_eine_leere_Beschreibung()
    {
        Assert.Equal(string.Empty, SkriptEintrag.BeschreibungAusText("import os\n", SkriptArt.Python));
    }

    [Fact]
    public void Leerer_Text_ergibt_eine_leere_Beschreibung()
    {
        Assert.Equal(string.Empty, SkriptEintrag.BeschreibungAusText("", SkriptArt.Python));
    }

    [Theory]
    [InlineData("a.py", SkriptArt.Python)]
    [InlineData("a.PS1", SkriptArt.PowerShell)]
    [InlineData("a.bat", SkriptArt.Batch)]
    [InlineData("a.cmd", SkriptArt.Batch)]
    [InlineData("a.txt", SkriptArt.Unbekannt)]
    public void Die_Art_ergibt_sich_aus_der_Endung(string name, SkriptArt erwartet)
    {
        Assert.Equal(erwartet, SkriptEintrag.ArtAusEndung(name));
    }
}

public class SkriptKatalogTests : IDisposable
{
    private readonly string _ordner =
        Path.Combine(Path.GetTempPath(), "dzskript_" + Guid.NewGuid().ToString("N"));

    public SkriptKatalogTests()
    {
        Directory.CreateDirectory(_ordner);
        Directory.CreateDirectory(Path.Combine(_ordner, "unter"));
        Directory.CreateDirectory(Path.Combine(_ordner, "bin"));

        File.WriteAllText(Path.Combine(_ordner, "eins.py"),
            "\"\"\"Erstes Skript, macht etwas mit Höhen.\"\"\"\nimport os\n");
        File.WriteAllText(Path.Combine(_ordner, "zwei.ps1"),
            "# Zweites Skript, sichert Ordner\n$x = 1\n");
        File.WriteAllText(Path.Combine(_ordner, "unter", "drei.bat"),
            ":: Drittes Skript\necho hallo\n");
        File.WriteAllText(Path.Combine(_ordner, "egal.txt"), "kein Skript");
        File.WriteAllText(Path.Combine(_ordner, "bin", "versteckt.py"), "# darf nicht auftauchen\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Findet_Skripte_und_uebergeht_andere_Dateien()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner]);

        Assert.Equal(3, katalog.Eintraege.Count);
        Assert.DoesNotContain(katalog.Eintraege, e => e.Dateiname == "egal.txt");
    }

    [Fact]
    public void Bauordner_werden_uebergangen()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner]);

        Assert.DoesNotContain(katalog.Eintraege, e => e.Dateiname == "versteckt.py");
    }

    [Fact]
    public void Ohne_Unterordner_wird_nur_die_oberste_Ebene_gelesen()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner], mitUnterordnern: false);

        Assert.Equal(2, katalog.Eintraege.Count);
        Assert.DoesNotContain(katalog.Eintraege, e => e.Dateiname == "drei.bat");
    }

    [Fact]
    public void Titel_und_Art_werden_uebernommen()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner]);

        var eins = katalog.Eintraege.Single(e => e.Dateiname == "eins.py");
        Assert.Equal(SkriptArt.Python, eins.Art);
        Assert.Contains("Erstes Skript", eins.Titel);

        var zwei = katalog.Eintraege.Single(e => e.Dateiname == "zwei.ps1");
        Assert.Equal(SkriptArt.PowerShell, zwei.Art);
        Assert.Contains("Zweites Skript", zwei.Titel);
    }

    [Fact]
    public void Die_Suche_findet_ueber_die_Beschreibung()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner]);

        var treffer = katalog.Suche("sichert").ToList();

        Assert.Single(treffer);
        Assert.Equal("zwei.ps1", treffer[0].Dateiname);
    }

    [Fact]
    public void Eine_leere_Suche_liefert_alles()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner]);

        Assert.Equal(katalog.Eintraege.Count, katalog.Suche("  ").Count());
    }

    [Fact]
    public void Ein_nicht_vorhandener_Ordner_fuehrt_nicht_zu_einer_Ausnahme()
    {
        Assert.Empty(SkriptKatalog.Erstellen([@"C:\gibt-es-nicht-4711"]).Eintraege);
    }

    [Fact]
    public void Der_Befehl_haengt_Pfad_und_Argumente_getrennt_an()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner]);
        var eins = katalog.Eintraege.Single(e => e.Dateiname == "eins.py");

        var (programm, args) = SkriptLauf.Befehl(eins, ["--test", "wert mit leerzeichen"]);

        Assert.Equal("py", programm);
        Assert.Equal(eins.Pfad, args[0]);
        Assert.Contains("wert mit leerzeichen", args);
    }

    [Fact]
    public void PowerShell_wird_ohne_Profil_und_mit_Bypass_gestartet()
    {
        var katalog = SkriptKatalog.Erstellen([_ordner]);
        var zwei = katalog.Eintraege.Single(e => e.Dateiname == "zwei.ps1");

        var (programm, args) = SkriptLauf.Befehl(zwei);

        Assert.Equal("powershell", programm);
        Assert.Contains("-NoProfile", args);
        Assert.Contains("Bypass", args);
        Assert.Contains(zwei.Pfad, args);
    }

    [Fact]
    public async Task Ein_Batchskript_laeuft_und_liefert_seine_Ausgabe()
    {
        var pfad = Path.Combine(_ordner, "hallo.bat");
        File.WriteAllText(pfad, ":: Testskript\r\n@echo off\r\necho HALLO-VOM-SKRIPT\r\nexit /b 0\r\n");

        var eintrag = SkriptEintrag.AusDatei(pfad);
        var zeilen = new List<AusgabeZeile>();
        var ausgabe = new Progress<AusgabeZeile>(z => { lock (zeilen) zeilen.Add(z); });

        var code = await SkriptLauf.AusfuehrenAsync(eintrag, ausgabe);

        // Progress<T> meldet asynchron; kurz nachfassen.
        for (var i = 0; i < 40 && zeilen.Count == 0; i++) await Task.Delay(25);

        Assert.Equal(0, code);
        lock (zeilen)
        {
            Assert.Contains(zeilen, z => z.Text.Contains("HALLO-VOM-SKRIPT"));
        }
    }

    [Fact]
    public async Task Ein_Rueckgabewert_ungleich_null_wird_durchgereicht()
    {
        var pfad = Path.Combine(_ordner, "fehler.bat");
        File.WriteAllText(pfad, "@echo off\r\nexit /b 3\r\n");

        var eintrag = SkriptEintrag.AusDatei(pfad);
        var code = await SkriptLauf.AusfuehrenAsync(eintrag, new Progress<AusgabeZeile>(_ => { }));

        Assert.Equal(3, code);
    }

    [Fact]
    public async Task Ein_fehlendes_Skript_wirft_FileNotFoundException()
    {
        var eintrag = new SkriptEintrag
        {
            Pfad = Path.Combine(_ordner, "gibt-es-nicht.bat"),
            Dateiname = "gibt-es-nicht.bat",
            Art = SkriptArt.Batch,
            Titel = "x",
            Beschreibung = string.Empty,
            Groesse = 0,
            GeaendertUtc = DateTime.UnixEpoch,
            Ordner = _ordner,
        };

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => SkriptLauf.AusfuehrenAsync(eintrag, new Progress<AusgabeZeile>(_ => { })));
    }

    [PDriveFact]
    public void Findet_die_echten_Hilfsskripte_des_Repositorys()
    {
        // Vom Testverzeichnis aus zum Repositoriumswurzelverzeichnis.
        var hier = AppContext.BaseDirectory;
        var wurzel = hier;
        for (var i = 0; i < 8 && wurzel is not null; i++)
        {
            if (Directory.Exists(Path.Combine(wurzel, "DayZ_Helper_Scripte"))) break;
            wurzel = Path.GetDirectoryName(wurzel);
        }

        var ordner = wurzel is null ? null : Path.Combine(wurzel, "DayZ_Helper_Scripte");
        if (ordner is null || !Directory.Exists(ordner)) return;

        var katalog = SkriptKatalog.Erstellen([ordner]);

        Assert.True(katalog.Eintraege.Count >= 10,
            $"Nur {katalog.Eintraege.Count} Skripte gefunden");
        Assert.Contains(katalog.Eintraege, e => e.Art == SkriptArt.Python);
        Assert.Contains(katalog.Eintraege, e => e.Art == SkriptArt.PowerShell);
        Assert.Contains(katalog.Eintraege, e => e.Beschreibung.Length > 0);
    }
}
