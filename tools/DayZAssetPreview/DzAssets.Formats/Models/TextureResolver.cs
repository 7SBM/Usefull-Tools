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

        if (Path.IsPathRooted(bereinigt))
        {
            // Liegt die Datei tatsaechlich dort, ist alles gut.
            if (DateiVorhanden(bereinigt)) return bereinigt;

            // Sonst ist es fast immer ein Verweis auf das Arma-Arbeits-
            // laufwerk "P:\", das hier nicht eingebunden ist. Ein
            // Arbeitslaufwerk ist ein Spiegel davon, also den
            // Laufwerksbuchstaben abstreifen und relativ weitersuchen.
            //
            // Findet sich dort ebenfalls nichts, bleibt der urspruengliche
            // Pfad die Antwort — der Aufrufer soll melden, was im Modell
            // steht, nicht einen von hier erfundenen Ort.
            var ohneLaufwerk = OhneLaufwerksbuchstaben(bereinigt);
            if (ohneLaufwerk is null) return bereinigt;

            return UnterWurzeln(ohneLaufwerk) ?? bereinigt;
        }

        if (_wurzeln.Length == 0) return null;

        return UnterWurzeln(bereinigt) ?? Path.Combine(_wurzeln[0], bereinigt);
    }

    /// <summary>Erste Wurzel, unter der die Datei tatsaechlich liegt.</summary>
    private string? UnterWurzeln(string relativ)
    {
        foreach (var wurzel in _wurzeln)
        {
            var kandidat = Path.Combine(wurzel, relativ);
            if (DateiVorhanden(kandidat)) return kandidat;
        }

        return null;
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

    /// <summary>
    /// Streift "P:\" oder einen anderen Laufwerksbuchstaben ab.
    /// Null, wenn der Pfad gar keinen hat (etwa ein UNC-Pfad).
    /// </summary>
    private static string? OhneLaufwerksbuchstaben(string pfad)
    {
        if (pfad.Length < 3) return null;
        if (pfad[1] != ':') return null;
        if (!char.IsLetter(pfad[0])) return null;

        return pfad[2..].TrimStart('\\');
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
