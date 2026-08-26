using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

public sealed class AssetVorschauModul : IWerkzeugModul
{
    public string Id => "asset-vorschau";
    public string Titel => "Asset-Vorschau";

    /// <summary>E809 — Wuerfel, Segoe Fluent Icons.</summary>
    public string Symbol => "";

    public string Beschreibung => "DayZ-Modelle aus den entpackten Gamefiles ansehen";

    public UserControl ErzeugeAnsicht(WerkzeugKontext kontext) => new AssetVorschauAnsicht(kontext);
}
