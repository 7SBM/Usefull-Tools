using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.Debinarizer;

public sealed class DebinarizerModul : IWerkzeugModul
{
    public string Id => "debinarizer";
    public string Titel => "Debinarizer";

    /// <summary>E8AB — Umwandeln, Segoe Fluent Icons.</summary>
    public string Symbol => "";

    public string Beschreibung => "Binarisierte P3D (ODOL) in bearbeitbare MLOD umwandeln";

    public UserControl ErzeugeAnsicht(WerkzeugKontext kontext) => new DebinarizerAnsicht(kontext);
}
