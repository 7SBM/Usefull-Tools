using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DzAssets.Formats.Models;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>
/// Uebersetzt einen Mesh-Abschnitt in eine WPF-Geometrie.
///
/// Umindiziert dabei: WPF verlangt gleich lange Listen fuer Positionen,
/// Normalen und Texturkoordinaten. Weil jeder Abschnitt nur einen Teil der
/// Vertizes benutzt, wird nur dieser Teil kopiert — bei einem Haus mit
/// zwanzig Abschnitten spart das gegenueber zwanzig vollstaendigen Kopien
/// erheblich Speicher.
/// </summary>
public static class GeometrieBauer
{
    public static MeshGeometry3D Bauen(LodGeometry lod, MeshSection abschnitt)
    {
        ArgumentNullException.ThrowIfNull(lod);
        ArgumentNullException.ThrowIfNull(abschnitt);

        var geometrie = new MeshGeometry3D();
        if (abschnitt.Indices.Length == 0) return geometrie;

        var positionen = new Point3DCollection(abschnitt.Indices.Length);
        var normalen = new Vector3DCollection(abschnitt.Indices.Length);
        var uvs = new PointCollection(abschnitt.Indices.Length);
        var dreiecke = new Int32Collection(abschnitt.Indices.Length);

        var abbildung = new Dictionary<int, int>(abschnitt.Indices.Length);

        foreach (var alt in abschnitt.Indices)
        {
            if (alt < 0 || alt >= lod.Positions.Length) continue;

            if (!abbildung.TryGetValue(alt, out var neu))
            {
                neu = positionen.Count;
                abbildung[alt] = neu;

                var p = lod.Positions[alt];
                positionen.Add(new Point3D(p.X, p.Y, p.Z));

                var n = alt < lod.Normals.Length ? lod.Normals[alt] : new Vec3(0, 1, 0);
                normalen.Add(new Vector3D(n.X, n.Y, n.Z));

                var uv = alt < lod.Uvs.Length ? lod.Uvs[alt] : new Vec2(0, 0);
                // WPF zaehlt V von oben, P3D von unten.
                uvs.Add(new Point(uv.U, 1.0 - uv.V));
            }

            dreiecke.Add(neu);
        }

        positionen.Freeze();
        normalen.Freeze();
        uvs.Freeze();
        dreiecke.Freeze();

        geometrie.Positions = positionen;
        geometrie.Normals = normalen;
        geometrie.TextureCoordinates = uvs;
        geometrie.TriangleIndices = dreiecke;
        geometrie.Freeze();
        return geometrie;
    }
}
