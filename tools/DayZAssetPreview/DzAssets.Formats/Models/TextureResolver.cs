using System.Collections.Concurrent;
using System.IO;

namespace DzAssets.Formats.Models;

/// <summary>
/// Findet zu einem Mesh-Abschnitt die zugehoerige Diffusetextur auf der
/// Platte. Reihenfolge: erst der Textur-Slot des Abschnitts, dann die
/// _co-Textur aus dem Material, dann dessen erste Stage.
/// </summary>
public sealed class TextureResolver
{
    private readonly ConcurrentDictionary<string, RvmatMaterial> _materialCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string[] _wurzeln;

    public TextureResolver(string pDrive) : this([pDrive]) { }

    /// <summary>
    /// Mehrere Wurzeln, damit Texturen eigener Mods gefunden werden, die
    /// nicht unterhalb desselben Ordners wie die Gamefiles liegen.
    /// </summary>
    public TextureResolver(IReadOnlyList<string> wurzeln)
    {
        ArgumentNullException.ThrowIfNull(wurzeln);
        _wurzeln = wurzeln.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
    }

    public IReadOnlyList<string> Wurzeln => _wurzeln;

    /// <summary>
    /// Wandelt "DZ\a\b_co.paa" in einen absoluten Pfad. Null bei leerer
    /// Eingabe. Existiert die Datei unter mehreren Wurzeln, gewinnt die
    /// erste, unter der sie tatsaechlich liegt; sonst die erste Wurzel.
    /// </summary>
    public string? ZuAbsolut(string? assetPfad)
    {
        if (string.IsNullOrWhiteSpace(assetPfad)) return null;

        var bereinigt = assetPfad.Trim().Replace('/', '\\').TrimStart('\\');
        if (bereinigt.Length == 0) return null;
        if (Path.IsPathRooted(bereinigt)) return bereinigt;
        if (_wurzeln.Length == 0) return null;

        string? ersterVersuch = null;
        foreach (var wurzel in _wurzeln)
        {
            var kandidat = Path.Combine(wurzel, bereinigt);
            ersterVersuch ??= kandidat;

            if (DateiVorhanden(kandidat)) return kandidat;
        }

        return ersterVersuch;
    }

    /// <summary>Absoluter Pfad zur Diffusetextur des Abschnitts, oder null.</summary>
    public string? DiffuseFuer(MeshSection abschnitt)
    {
        ArgumentNullException.ThrowIfNull(abschnitt);

        var direkt = ZuAbsolut(abschnitt.TexturePath);
        if (direkt is not null && DateiVorhanden(direkt)) return direkt;

        var materialPfad = ZuAbsolut(abschnitt.MaterialPath);
        if (materialPfad is null || !DateiVorhanden(materialPfad)) return null;

        var material = _materialCache.GetOrAdd(materialPfad, RvmatMaterial.Load);
        var ausMaterial = ZuAbsolut(material.DiffusePfad);

        return ausMaterial is not null && DateiVorhanden(ausMaterial) ? ausMaterial : null;
    }

    private static bool DateiVorhanden(string pfad)
    {
        try
        {
            return File.Exists(pfad);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
