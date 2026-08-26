using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

public sealed class AssetVorschauModul : IWerkzeugModul
{
    public string Id => "objekt-preview";
    public string Titel => "Objekt Preview";

    /// <summary>E809 — Wuerfel, Segoe Fluent Icons.</summary>
    public string Symbol => "";

    public string Beschreibung => "DayZ-Modelle aus den entpackten Gamefiles ansehen";

    /// <summary>
    /// Modell, das nach dem Start sofort geladen wird. Wird aus dem
    /// Aufrufargument gesetzt, damit sich eine .p3d direkt oeffnen laesst.
    /// </summary>
    public string? SofortOeffnen { get; init; }

    /// <summary>Suchtext, mit dem das Suchfeld vorbelegt wird.</summary>
    public string? SofortSuchen { get; init; }

    public UserControl ErzeugeAnsicht(WerkzeugKontext kontext)
        => new AssetVorschauAnsicht(kontext, SofortOeffnen, SofortSuchen);
}
