using DzAssets.Formats.Models;

namespace DzAssets.Tests;

public class P3dModelReaderTests
{
    private const string Waschbecken = @"structures\furniture\bathroom\basin_a\basin_a.p3d";

    private static ModelGeometry Modell() => P3dModelReader.Read(TestAssets.Dz(Waschbecken));

    [PDriveFact]
    public void Liest_LODs_mit_Geometrie()
    {
        var modell = Modell();

        Assert.NotEmpty(modell.Lods);
        Assert.NotNull(modell.FeinsterSichtbarerLod);
        Assert.NotEmpty(modell.FeinsterSichtbarerLod!.Positions);
    }

    [PDriveFact]
    public void Jeder_Index_liegt_innerhalb_der_Vertexliste()
    {
        var modell = Modell();

        foreach (var lod in modell.Lods)
        {
            foreach (var abschnitt in lod.Sections)
            {
                Assert.True(abschnitt.Indices.Length % 3 == 0,
                    $"LOD {lod.Name}: Indexanzahl {abschnitt.Indices.Length} ist kein Vielfaches von 3");

                foreach (var index in abschnitt.Indices)
                    Assert.InRange(index, 0, lod.Positions.Length - 1);
            }
        }
    }

    [PDriveFact]
    public void Die_Bounding_Box_ist_endlich_und_plausibel()
    {
        var modell = Modell();

        Assert.True(float.IsFinite(modell.Size.X));
        Assert.True(float.IsFinite(modell.Size.Y));
        Assert.True(float.IsFinite(modell.Size.Z));
        // Ein Waschbecken ist groesser als ein Zentimeter und kleiner als ein Haus.
        Assert.InRange(modell.Size.X, 0.01f, 20f);
        Assert.InRange(modell.Size.Y, 0.01f, 20f);
        Assert.InRange(modell.Size.Z, 0.01f, 20f);
    }

    [PDriveFact]
    public void UV_Koordinaten_und_Normalen_gibt_es_fuer_jeden_Vertex()
    {
        var lod = Modell().FeinsterSichtbarerLod!;

        Assert.Equal(lod.Positions.Length, lod.Uvs.Length);
        Assert.Equal(lod.Positions.Length, lod.Normals.Length);
    }

    [PDriveFact]
    public void Mindestens_ein_Abschnitt_nennt_eine_Textur_oder_ein_Material()
    {
        var lod = Modell().FeinsterSichtbarerLod!;

        Assert.Contains(lod.Sections, a => !string.IsNullOrEmpty(a.TexturePath)
                                        || !string.IsNullOrEmpty(a.MaterialPath));
    }

    [PDriveFact]
    public void Sichtbare_und_technische_LODs_werden_unterschieden()
    {
        var modell = Modell();

        Assert.Contains(modell.Lods, l => l.IstSichtbar);
        // Fast jedes DayZ-Modell hat einen Geometry- oder Memory-LOD.
        Assert.Contains(modell.Lods, l => !l.IstSichtbar);
    }

    [PDriveFact]
    public void Der_feinste_sichtbare_LOD_hat_die_kleinste_Aufloesungszahl()
    {
        var modell = Modell();
        var gewaehlt = modell.FeinsterSichtbarerLod!;

        var sichtbare = modell.Lods.Where(l => l.IstSichtbar && l.Positions.Length > 0);
        Assert.All(sichtbare, l => Assert.True(l.Resolution >= gewaehlt.Resolution));
    }

    [PDriveFact]
    public void Die_Dreiecksanzahl_stimmt_mit_der_Indexanzahl_ueberein()
    {
        var lod = Modell().FeinsterSichtbarerLod!;

        var ausIndizes = lod.Sections.Sum(a => a.Indices.Length) / 3;
        Assert.Equal(ausIndizes, lod.TriangleCount);
        Assert.True(lod.TriangleCount > 0);
    }

    [PDriveFact]
    public void Eine_fehlende_Datei_wirft_FileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(
            () => P3dModelReader.Read(@"C:\gibt-es-nicht\modell.p3d"));
    }

    [PDriveFact]
    public void Liest_auch_debinarisierte_MLOD_Modelle()
    {
        // Auf dem Arbeitslaufwerk liegen neben den ausgelieferten
        // ODOL-Dateien auch debinarisierte MLOD-Kopien.
        var pfad = TestAssets.Dz(@"structures\tracks\rail_tracke_2.p3d");
        Assert.True(File.Exists(pfad), $"Testdatei fehlt: {pfad}");

        var modell = P3dModelReader.Read(pfad);

        Assert.False(modell.IstBinarisiert);
        Assert.NotEmpty(modell.Lods);

        var lod = modell.FeinsterSichtbarerLod;
        Assert.NotNull(lod);
        Assert.NotEmpty(lod!.Positions);
        Assert.NotEmpty(lod.Sections);
        Assert.Equal(lod.Positions.Length, lod.Uvs.Length);
        Assert.Equal(lod.Positions.Length, lod.Normals.Length);

        foreach (var abschnitt in lod.Sections)
            foreach (var index in abschnitt.Indices)
                Assert.InRange(index, 0, lod.Positions.Length - 1);
    }

    [PDriveFact]
    public void Ein_MLOD_Abschnitt_nennt_Textur_und_Material_direkt()
    {
        var modell = P3dModelReader.Read(TestAssets.Dz(@"structures\tracks\rail_tracke_2.p3d"));
        var lod = modell.FeinsterSichtbarerLod!;

        Assert.Contains(lod.Sections, a => !string.IsNullOrEmpty(a.TexturePath)
                                        || !string.IsNullOrEmpty(a.MaterialPath));
    }

    [PDriveFact]
    public void Das_ausgelieferte_Waschbecken_ist_binarisiert()
    {
        Assert.True(Modell().IstBinarisiert);
    }

    [PDriveFact]
    public void Hundert_echte_Modelle_werden_ohne_Ausnahme_gelesen()
    {
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ", "structures");
        var dateien = Directory.EnumerateFiles(wurzel, "*.p3d", SearchOption.AllDirectories)
            .Take(100)
            .ToList();

        Assert.NotEmpty(dateien);

        var fehler = new List<string>();
        foreach (var datei in dateien)
        {
            try
            {
                var modell = P3dModelReader.Read(datei);
                Assert.NotEmpty(modell.Lods);
            }
            catch (Exception ausnahme)
            {
                fehler.Add($"{Path.GetFileName(datei)}: {ausnahme.GetType().Name} {ausnahme.Message}");
            }
        }

        Assert.True(fehler.Count == 0,
            $"{fehler.Count} von {dateien.Count} Modellen schlugen fehl:{Environment.NewLine}"
            + string.Join(Environment.NewLine, fehler.Take(10)));
    }

    [PDriveFact]
    public void Vierhundert_echte_Modelle_liefern_brauchbare_Geometrie()
    {
        // Breitere Stichprobe ueber mehrere Ordner, damit beide Formate
        // und verschiedene Modellarten vorkommen.
        var wurzel = Path.Combine(TestAssets.PDrive!, "DZ");
        var dateien = new[] { "structures", "plants", "rocks", "vehicles" }
            .Select(o => Path.Combine(wurzel, o))
            .Where(Directory.Exists)
            .SelectMany(o => Directory.EnumerateFiles(o, "*.p3d", SearchOption.AllDirectories).Take(100))
            .ToList();

        Assert.NotEmpty(dateien);

        var fehler = new List<string>();
        var ohneSichtbarenLod = 0;

        foreach (var datei in dateien)
        {
            try
            {
                var modell = P3dModelReader.Read(datei);
                if (modell.FeinsterSichtbarerLod is null) { ohneSichtbarenLod++; continue; }

                var lod = modell.FeinsterSichtbarerLod;
                Assert.Equal(lod.Positions.Length, lod.Uvs.Length);
                Assert.Equal(lod.Positions.Length, lod.Normals.Length);

                foreach (var abschnitt in lod.Sections)
                {
                    Assert.True(abschnitt.Indices.Length % 3 == 0);
                    foreach (var index in abschnitt.Indices)
                        Assert.InRange(index, 0, lod.Positions.Length - 1);
                }
            }
            catch (Exception ausnahme)
            {
                fehler.Add($"{Path.GetFileName(datei)}: {ausnahme.GetType().Name} {ausnahme.Message}");
            }
        }

        Assert.True(fehler.Count == 0,
            $"{fehler.Count} von {dateien.Count} Modellen schlugen fehl:{Environment.NewLine}"
            + string.Join(Environment.NewLine, fehler.Take(10)));

        // Ein paar reine Proxy- oder Memory-Modelle sind normal, aber nicht die Mehrheit.
        Assert.True(ohneSichtbarenLod < dateien.Count / 4,
            $"{ohneSichtbarenLod} von {dateien.Count} Modellen haben keinen sichtbaren LOD.");
    }
}
