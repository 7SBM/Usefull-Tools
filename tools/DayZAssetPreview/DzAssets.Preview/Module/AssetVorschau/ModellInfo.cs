using System.Globalization;
// UseWPF nimmt System.IO aus den impliziten Usings, weil Path sonst mit
// System.Windows.Shapes.Path kollidieren wuerde.
using System.IO;
using DzAssets.Formats.Models;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>Die Angaben, die rechts neben dem Viewport stehen.</summary>
public sealed class ModellInfo : BeobachtbaresObjekt
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private ModellInfo() { }

    public string Dateiname { get; private init; } = string.Empty;
    public string AssetPfad { get; private init; } = string.Empty;
    public string Groesse { get; private init; } = string.Empty;
    public string Dreiecke { get; private init; } = string.Empty;
    public string Version { get; private init; } = string.Empty;
    public string Klassen { get; private init; } = "—";
    public IReadOnlyList<LodGeometry> Lods { get; private init; } = [];
    public IReadOnlyList<string> FehlendeTexturen { get; private init; } = [];

    public bool HatFehlendeTexturen => FehlendeTexturen.Count > 0;

    public string FehlendeTexturenText => FehlendeTexturen.Count == 0
        ? string.Empty
        : $"{FehlendeTexturen.Count} Textur(en) nicht gefunden:{Environment.NewLine}"
          + string.Join(Environment.NewLine, FehlendeTexturen.Take(6))
          + (FehlendeTexturen.Count > 6 ? $"{Environment.NewLine}…" : string.Empty);

    public static ModellInfo Erzeugen(
        ModelGeometry modell,
        LodGeometry lod,
        string assetPfad,
        IReadOnlyList<string> klassen,
        IReadOnlyList<string> fehlendeTexturen)
    {
        ArgumentNullException.ThrowIfNull(modell);
        ArgumentNullException.ThrowIfNull(lod);

        var groesse = modell.Size;

        return new ModellInfo
        {
            Dateiname = Path.GetFileNameWithoutExtension(modell.Path),
            AssetPfad = assetPfad,
            Groesse = string.Format(Deutsch, "{0:N2} × {1:N2} × {2:N2} m",
                groesse.X, groesse.Y, groesse.Z),
            Dreiecke = lod.TriangleCountOhneProxy == lod.TriangleCount
                ? lod.TriangleCount.ToString("N0", Deutsch)
                : string.Format(Deutsch, "{0:N0}  (+{1:N0} Proxy)",
                    lod.TriangleCountOhneProxy,
                    lod.TriangleCount - lod.TriangleCountOhneProxy),
            Version = modell.IstBinarisiert
                ? $"ODOL {modell.Version}"
                : $"MLOD {modell.Version} (debinarisiert)",
            Klassen = klassen.Count == 0 ? "—" : string.Join(", ", klassen),
            Lods = modell.Lods,
            FehlendeTexturen = fehlendeTexturen,
        };
    }
}
