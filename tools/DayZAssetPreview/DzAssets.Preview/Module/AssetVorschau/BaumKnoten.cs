using System.Collections.ObjectModel;
// UseWPF nimmt System.IO aus den impliziten Usings, weil Path sonst mit
// System.Windows.Shapes.Path kollidieren wuerde.
using System.IO;
using DzAssets.Formats.Katalog;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

/// <summary>
/// Ein Ordner oder ein Modell im Bestandsbaum. Der Baum wird aus dem
/// bereits eingelesenen Index gebaut und nicht beim Aufklappen erneut von
/// der Platte gelesen — das waere langsamer und zeigte Ordner ohne
/// Modelle, die hier niemanden interessieren.
/// </summary>
public sealed class BaumKnoten : BeobachtbaresObjekt
{
    private bool _istAufgeklappt;

    private BaumKnoten(string beschriftung, bool istOrdner, string? assetPfad, string? absoluterPfad)
    {
        Beschriftung = beschriftung;
        IstOrdner = istOrdner;
        AssetPfad = assetPfad;
        AbsoluterPfad = absoluterPfad;
    }

    public string Beschriftung { get; }
    public bool IstOrdner { get; }
    public string? AssetPfad { get; }
    public string? AbsoluterPfad { get; }
    public ObservableCollection<BaumKnoten> Kinder { get; } = [];

    /// <summary>E8B7 = Ordner, E809 = Wuerfel (Segoe Fluent Icons).</summary>
    public string Symbol => IstOrdner ? "" : "";

    public bool IstAufgeklappt
    {
        get => _istAufgeklappt;
        set => Setzen(ref _istAufgeklappt, value);
    }

    public static IReadOnlyList<BaumKnoten> BaumBauen(IEnumerable<AssetEintrag> eintraege)
    {
        ArgumentNullException.ThrowIfNull(eintraege);

        var wurzeln = new List<BaumKnoten>();
        var wurzelNachName = new Dictionary<string, BaumKnoten>(StringComparer.OrdinalIgnoreCase);
        var ordnerCache = new Dictionary<string, BaumKnoten>(StringComparer.OrdinalIgnoreCase);

        foreach (var eintrag in eintraege)
        {
            var teile = eintrag.AssetPfad.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (teile.Length == 0) continue;

            BaumKnoten? eltern = null;
            var bisher = string.Empty;

            for (var i = 0; i < teile.Length - 1; i++)
            {
                bisher = bisher.Length == 0 ? teile[i] : bisher + "\\" + teile[i];

                if (!ordnerCache.TryGetValue(bisher, out var ordner))
                {
                    ordner = new BaumKnoten(teile[i], istOrdner: true, null, null);
                    ordnerCache[bisher] = ordner;

                    if (eltern is null)
                    {
                        wurzeln.Add(ordner);
                        wurzelNachName[bisher] = ordner;
                    }
                    else
                    {
                        eltern.Kinder.Add(ordner);
                    }
                }

                eltern = ordner;
            }

            var blatt = new BaumKnoten(
                Path.GetFileNameWithoutExtension(teile[^1]),
                istOrdner: false,
                eintrag.AssetPfad,
                eintrag.AbsoluterPfad);

            if (eltern is null) wurzeln.Add(blatt);
            else eltern.Kinder.Add(blatt);
        }

        foreach (var knoten in ordnerCache.Values) Sortieren(knoten);
        wurzeln.Sort(Vergleichen);
        return wurzeln;
    }

    private static void Sortieren(BaumKnoten knoten)
    {
        if (knoten.Kinder.Count < 2) return;

        var sortiert = knoten.Kinder.ToList();
        sortiert.Sort(Vergleichen);

        knoten.Kinder.Clear();
        foreach (var kind in sortiert) knoten.Kinder.Add(kind);
    }

    private static int Vergleichen(BaumKnoten? a, BaumKnoten? b)
    {
        if (a is null || b is null) return 0;
        if (a.IstOrdner != b.IstOrdner) return a.IstOrdner ? -1 : 1;
        return string.Compare(a.Beschriftung, b.Beschriftung, StringComparison.OrdinalIgnoreCase);
    }
}
