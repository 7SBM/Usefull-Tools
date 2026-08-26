using System.IO;
using System.Text.RegularExpressions;

namespace DzAssets.Formats.Models;

/// <summary>
/// Liest die Texturzuweisungen aus einer Klartext-RVMAT.
/// Binarisierte RVMAT (Kennung "raP") werden nicht unterstuetzt; in den
/// DayZ-Gamefiles kommen sie nicht vor (am 2026-08-26 geprueft).
/// </summary>
public sealed partial class RvmatMaterial
{
    private RvmatMaterial(IReadOnlyList<string> stageTexturen)
    {
        StageTextures = stageTexturen;
        DiffusePfad = stageTexturen.FirstOrDefault(
                          t => t.EndsWith("_co.paa", StringComparison.OrdinalIgnoreCase))
                      ?? stageTexturen.FirstOrDefault();
    }

    public IReadOnlyList<string> StageTextures { get; }
    public string? DiffusePfad { get; }

    public static RvmatMaterial Leer { get; } = new([]);

    public static RvmatMaterial Parse(string text)
    {
        if (string.IsNullOrEmpty(text) || text.StartsWith("raP", StringComparison.Ordinal))
            return Leer;

        var treffer = TexturZeile()
            .Matches(text)
            .Select(m => m.Groups["pfad"].Value)
            .Where(p => p.Length > 0)
            .Where(p => !p.StartsWith('#'))          // prozedurale Texturen
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return treffer.Count == 0 ? Leer : new RvmatMaterial(treffer);
    }

    public static RvmatMaterial Load(string pfad)
    {
        try
        {
            return Parse(File.ReadAllText(pfad));
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            return Leer;
        }
    }

    [GeneratedRegex("""texture\s*=\s*"(?<pfad>[^"]*)"\s*;""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TexturZeile();
}
