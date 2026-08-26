namespace DzAssets.Formats.Models;

public readonly record struct Vec3(float X, float Y, float Z)
{
    public static Vec3 Min(Vec3 a, Vec3 b)
        => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Min(a.Z, b.Z));

    public static Vec3 Max(Vec3 a, Vec3 b)
        => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Max(a.Z, b.Z));

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}

public readonly record struct Vec2(float U, float V);

/// <summary>Ein Teil eines LODs mit eigener Textur beziehungsweise eigenem Material.</summary>
public sealed class MeshSection
{
    public required int[] Indices { get; init; }

    /// <summary>Assetpfad der Diffusetextur, z. B. "DZ\structures\...\x_co.paa".</summary>
    public string? TexturePath { get; init; }

    /// <summary>Assetpfad des Materials, z. B. "DZ\structures\...\x.rvmat".</summary>
    public string? MaterialPath { get; init; }

    public int TriangleCount => Indices.Length / 3;
}

public sealed class LodGeometry
{
    public required float Resolution { get; init; }
    public required string Name { get; init; }
    public required bool IstSichtbar { get; init; }
    public required Vec3[] Positions { get; init; }
    public required Vec3[] Normals { get; init; }
    public required Vec2[] Uvs { get; init; }
    public required MeshSection[] Sections { get; init; }

    public int TriangleCount => Sections.Sum(a => a.TriangleCount);

    /// <summary>
    /// Der LOD-Name. Wird von der Oberflaeche benutzt, damit eine
    /// Auswahlliste den Namen zeigt und nicht den Klassennamen — auch
    /// dann, wenn eine Vorlage DisplayMemberPath nicht auswertet.
    /// </summary>
    public override string ToString() => Name;
}

public sealed class ModelGeometry
{
    public required string Path { get; init; }
    public required uint Version { get; init; }

    /// <summary>Wahr bei ODOL (binarisiert), falsch bei MLOD (debinarisiert).</summary>
    public required bool IstBinarisiert { get; init; }

    public required LodGeometry[] Lods { get; init; }
    public required Vec3 BoundsMin { get; init; }
    public required Vec3 BoundsMax { get; init; }

    /// <summary>Ausdehnung in Metern (X = Breite, Y = Hoehe, Z = Tiefe).</summary>
    public Vec3 Size => BoundsMax - BoundsMin;

    /// <summary>
    /// Der sichtbare LOD mit der kleinsten Aufloesungszahl, also der feinste.
    /// Null, wenn das Modell nur technische LODs enthaelt.
    /// </summary>
    public LodGeometry? FeinsterSichtbarerLod
        => Lods.Where(l => l.IstSichtbar && l.Positions.Length > 0)
               .OrderBy(l => l.Resolution)
               .FirstOrDefault();
}
