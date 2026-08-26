using DzAssets.Formats.Models;
using DzAssets.Preview.Module.AssetVorschau;

namespace DzAssets.Tests;

public class GeometrieBauerTests
{
    private static LodGeometry EinfacherLod() => new()
    {
        Resolution = 1.0f,
        Name = "1.000",
        IstSichtbar = true,
        Positions = [new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(9, 9, 9)],
        Normals = [new Vec3(0, 0, 1), new Vec3(0, 0, 1), new Vec3(0, 0, 1), new Vec3(0, 1, 0)],
        Uvs = [new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1), new Vec2(0.5f, 0.5f)],
        Sections = [],
    };

    [Fact]
    public void Baut_ein_Dreieck_mit_drei_Vertizes()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        Assert.Equal(3, geometrie.Positions.Count);
        Assert.Equal(3, geometrie.TriangleIndices.Count);
        Assert.Equal(3, geometrie.Normals.Count);
        Assert.Equal(3, geometrie.TextureCoordinates.Count);
    }

    [Fact]
    public void Nicht_benutzte_Vertizes_werden_weggelassen()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        // Vertex 3 (9,9,9) wird vom Abschnitt nicht benutzt.
        Assert.DoesNotContain(geometrie.Positions, p => p.X == 9);
    }

    [Fact]
    public void Die_V_Koordinate_wird_gespiegelt()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        // Vertex 2 hatte v = 1 -> in WPF 0
        Assert.Equal(0.0, geometrie.TextureCoordinates[2].Y, precision: 5);
        // Vertex 0 hatte v = 0 -> in WPF 1
        Assert.Equal(1.0, geometrie.TextureCoordinates[0].Y, precision: 5);
    }

    [Fact]
    public void Die_U_Koordinate_bleibt_unveraendert()
    {
        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), new MeshSection { Indices = [0, 1, 2] });

        Assert.Equal(1.0, geometrie.TextureCoordinates[1].X, precision: 5);
    }

    [Fact]
    public void Ein_wiederholt_benutzter_Vertex_wird_nur_einmal_kopiert()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 2, 0, 2, 1] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        Assert.Equal(3, geometrie.Positions.Count);
        Assert.Equal(6, geometrie.TriangleIndices.Count);
    }

    [Fact]
    public void Ein_leerer_Abschnitt_ergibt_eine_leere_Geometrie()
    {
        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), new MeshSection { Indices = [] });

        Assert.Empty(geometrie.Positions);
    }

    [Fact]
    public void Indizes_ausserhalb_der_Vertexliste_werden_uebergangen()
    {
        var abschnitt = new MeshSection { Indices = [0, 1, 99] };

        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), abschnitt);

        Assert.Equal(2, geometrie.Positions.Count);
        Assert.All(geometrie.TriangleIndices, i => Assert.InRange(i, 0, 1));
    }

    [Fact]
    public void Die_Geometrie_ist_eingefroren_und_damit_threaduebergreifend_nutzbar()
    {
        var geometrie = GeometrieBauer.Bauen(EinfacherLod(), new MeshSection { Indices = [0, 1, 2] });

        Assert.True(geometrie.IsFrozen);
    }

    [PDriveFact]
    public void Ein_echtes_Modell_ergibt_eine_gueltige_Geometrie()
    {
        var modell = P3dModelReader.Read(
            TestAssets.Dz(@"structures\furniture\bathroom\basin_a\basin_a.p3d"));
        var lod = modell.FeinsterSichtbarerLod!;

        var gesamtDreiecke = 0;
        foreach (var abschnitt in lod.Sections)
        {
            var geometrie = GeometrieBauer.Bauen(lod, abschnitt);
            Assert.Equal(geometrie.Positions.Count, geometrie.Normals.Count);
            Assert.Equal(geometrie.Positions.Count, geometrie.TextureCoordinates.Count);
            Assert.True(geometrie.TriangleIndices.Count % 3 == 0);
            gesamtDreiecke += geometrie.TriangleIndices.Count / 3;
        }

        Assert.Equal(lod.TriangleCount, gesamtDreiecke);
    }
}
