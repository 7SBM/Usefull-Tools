using BisDll.Model;

namespace DzAssets.Tests;

public class BisDllSmokeTests
{
    private const string Waschbecken = @"structures\furniture\bathroom\basin_a\basin_a.p3d";

    [PDriveFact]
    public void Liest_eine_echte_DayZ_p3d_als_ODOL()
    {
        var pfad = TestAssets.Dz(Waschbecken);

        var modell = P3D.GetInstance(pfad);

        Assert.IsType<BisDll.Model.ODOL.ODOL>(modell);
        Assert.InRange(modell.Version, 28u, 75u);
        Assert.NotEmpty(modell.LODs);
    }

    [PDriveFact]
    public void Der_feinste_sichtbare_LOD_hat_Vertizes_und_Flaechen()
    {
        var pfad = TestAssets.Dz(Waschbecken);

        var modell = (BisDll.Model.ODOL.ODOL)P3D.GetInstance(pfad);
        var lod = modell.LODs
            .Cast<BisDll.Model.ODOL.LOD>()
            .Where(l => Resolution.IsResolution(l.Resolution))
            .OrderBy(l => l.Resolution)
            .First();

        Assert.True(lod.VertexCount > 0);
        Assert.True(lod.PolygonCount > 0);
        Assert.NotEmpty(lod.Sections);
    }
}
