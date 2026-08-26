using System.Windows.Media;
using DzAssets.Formats.Katalog;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>Ein Suchtreffer samt nachgeladenem Vorschaubild.</summary>
public sealed class TrefferAnzeige(AssetEintrag eintrag) : BeobachtbaresObjekt
{
    private ImageSource? _vorschau;

    public AssetEintrag Eintrag { get; } = eintrag;

    public ImageSource? Vorschau
    {
        get => _vorschau;
        set => Setzen(ref _vorschau, value);
    }
}
