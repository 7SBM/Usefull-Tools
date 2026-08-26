// Dieses Projekt kompiliert den Quellcode von BisDll aus
//   DayZ_Arma_p3dDeBin/_SOURCE/BisDll_Quellcode_DayZ+Arma3_FIXED/
// direkt mit (siehe DzAssets.Formats.csproj, Eigenschaft BisDllSrc).
//
// Bewusst keine Kopie: Korrekturen am ODOL-Parser des Debinarizers
// wirken damit unmittelbar auch in der Vorschau. Siehe VENDOR.md im
// Wurzelverzeichnis des Repositorys fuer die Herkunftsangabe.
namespace DzAssets.Formats;

internal static class Vendor
{
    internal const string BisDllHinweis =
        "ODOL-/MLOD-Lesen stammt aus BisDll, siehe VENDOR.md";
}
