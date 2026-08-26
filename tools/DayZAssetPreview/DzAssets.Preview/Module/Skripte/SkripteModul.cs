using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.Skripte;

public sealed class SkripteModul : IWerkzeugModul
{
    public string Id => "skripte";
    public string Titel => "Skripte";

    /// <summary>E756 — Befehlszeile, Segoe Fluent Icons.</summary>
    public string Symbol => "";

    public string Beschreibung => "Hilfsskripte ansehen, bearbeiten und ausführen";

    public UserControl ErzeugeAnsicht(WerkzeugKontext kontext) => new SkripteAnsicht(kontext);
}
