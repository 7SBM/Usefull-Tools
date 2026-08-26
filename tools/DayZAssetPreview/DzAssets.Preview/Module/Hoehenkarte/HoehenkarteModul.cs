using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.Hoehenkarte;

public sealed class HoehenkarteModul : IWerkzeugModul
{
    public string Id => "hoehenkarte";
    public string Titel => "Höhenkarte";

    /// <summary>E909 — Berge, Segoe Fluent Icons.</summary>
    public string Symbol => "";

    public string Beschreibung => "ASC-Höhenkarten in 3D ansehen";

    /// <summary>Karte, die nach dem Start sofort gelesen wird.</summary>
    public string? SofortOeffnen { get; init; }

    public UserControl ErzeugeAnsicht(WerkzeugKontext kontext)
        => new HoehenkarteAnsicht(kontext, SofortOeffnen);
}
