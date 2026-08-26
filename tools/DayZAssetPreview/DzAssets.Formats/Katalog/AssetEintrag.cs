namespace DzAssets.Formats.Katalog;

/// <summary>Ein gefundenes Modell im Bestand.</summary>
/// <param name="AbsoluterPfad">z. B. "H:\P_Drive\DZ\structures\...\basin_a.p3d"</param>
/// <param name="AssetPfad">relativ zur Wurzel, z. B. "DZ\structures\...\basin_a.p3d"</param>
/// <param name="Name">Dateiname ohne Endung, z. B. "basin_a"</param>
/// <param name="Ordner">Ordner relativ zur Wurzel, z. B. "DZ\structures\furniture"</param>
public sealed record AssetEintrag(
    string AbsoluterPfad,
    string AssetPfad,
    string Name,
    string Ordner,
    long Groesse,
    DateTime GeaendertUtc);
