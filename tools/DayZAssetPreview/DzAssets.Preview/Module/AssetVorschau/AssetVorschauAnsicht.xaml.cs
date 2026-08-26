using System.Windows.Controls;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

public partial class AssetVorschauAnsicht : UserControl
{
    private readonly WerkzeugKontext _kontext;

    public AssetVorschauAnsicht(WerkzeugKontext kontext)
    {
        _kontext = kontext;
        InitializeComponent();
    }
}
