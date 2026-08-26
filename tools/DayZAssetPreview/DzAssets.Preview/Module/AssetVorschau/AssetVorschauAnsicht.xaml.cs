// UseWPF nimmt System.IO aus den impliziten Usings, weil Path sonst mit
// System.Windows.Shapes.Path kollidieren wuerde.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DzAssets.Formats.Katalog;
using DzAssets.Formats.Models;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.AssetVorschau;

public partial class AssetVorschauAnsicht : UserControl
{
    private readonly WerkzeugKontext _kontext;
    private readonly TexturLader _texturLader;
    private readonly DispatcherTimer _suchTakt = new()
    {
        Interval = TimeSpan.FromMilliseconds(220),
    };

    private readonly ThumbnailService _bilder;
    private readonly Queue<TrefferAnzeige> _bildWarteschlange = new();
    private bool _bilderLaufen;

    private AssetIndex? _index;
    private ConfigClassIndex? _klassenIndex;

    private ModelGeometry? _aktuellesModell;
    private string _aktuellerAssetPfad = string.Empty;
    private bool _lodWirdGesetzt;

    /// <summary>
    /// Solange falsch, schreibt Ansicht_Geaendert nichts in die
    /// Einstellungen zurueck. Ohne das ueberschreibt das Setzen des ersten
    /// Schalters die Werte der noch nicht gesetzten — der zweite Schalter
    /// laese dann seinen eigenen, gerade zerstoerten Wert.
    /// </summary>
    private bool _initialisiert;

    private readonly string? _sofortOeffnen;
    private readonly string? _sofortSuchen;

    public AssetVorschauAnsicht(WerkzeugKontext kontext,
                                string? sofortOeffnen = null,
                                string? sofortSuchen = null)
    {
        _kontext = kontext;
        _sofortOeffnen = sofortOeffnen;
        _sofortSuchen = sofortSuchen;
        InitializeComponent();

        _texturLader = new TexturLader(
            new TextureResolver(_kontext.Einstellungen.Wurzeln),
            _kontext.Protokoll);

        // Eigener Lader fuer die Miniaturbilder: sonst wuerde die Liste
        // "Fehlend" des Hauptladers waehrend der Anzeige ueberschrieben.
        _bilder = new ThumbnailService(
            Path.Combine(_kontext.DatenOrdner, "thumbs"),
            new TexturLader(new TextureResolver(_kontext.Einstellungen.Wurzeln), _kontext.Protokoll),
            _kontext.Protokoll);

        SchalterGitter.IsChecked = _kontext.Einstellungen.BodengitterZeigen;
        SchalterMassstab.IsChecked = _kontext.Einstellungen.MassstabsfigurZeigen;
        SchalterDraht.IsChecked = _kontext.Einstellungen.DrahtgitterZeigen;
        SchalterProxys.IsChecked = _kontext.Einstellungen.ProxysZeigen;
        _initialisiert = true;
        AnsichtUebernehmen();

        _suchTakt.Tick += (_, _) => { _suchTakt.Stop(); SucheAusfuehren(); };

        Loaded += (_, _) => BestandLaden(neuEinlesen: false);
    }

    // -------------------------------------------------------- Bestand

    private async void BestandLaden(bool neuEinlesen)
    {
        KnopfNeuEinlesen.IsEnabled = false;
        BestandText.Text = "wird eingelesen…";

        var wurzeln = _kontext.Einstellungen.Wurzeln.ToList();
        var cacheDatei = _kontext.DatenDatei("index.json");

        if (wurzeln.Count == 0)
        {
            BestandText.Text = "kein Arbeitslaufwerk gefunden";
            ViewportHinweis.Text =
                "Kein Arbeitslaufwerk mit entpackten Gamefiles gefunden.\n\n" +
                $"Wurzeln eintragen in:\n{_kontext.DatenDatei(WerkzeugKontext.EinstellungsDatei)}";
            KnopfNeuEinlesen.IsEnabled = true;
            return;
        }

        try
        {
            _index = await Task.Run(() =>
            {
                if (!neuEinlesen)
                {
                    var ausCache = AssetIndex.AusCache(cacheDatei, wurzeln);
                    if (ausCache is not null) return ausCache;
                }

                var neu = AssetIndex.Erstellen(wurzeln);
                neu.InCacheSchreiben(cacheDatei);
                return neu;
            });

            var baum = await Task.Run(() => BaumKnoten.BaumBauen(_index.Eintraege));
            Baum.ItemsSource = baum;

            BestandText.Text = _index.Eintraege.Count == 0
                ? "keine Modelle gefunden — Wurzeln prüfen"
                : $"{_index.Eintraege.Count:N0} Modelle";

            _kontext.Protokoll.Schreiben($"Bestand eingelesen: {_index.Eintraege.Count} Modelle");

            if (!neuEinlesen && !string.IsNullOrWhiteSpace(_sofortSuchen))
            {
                SuchFeld.Text = _sofortSuchen;
                _suchTakt.Stop();
                SucheAusfuehren();
            }

            if (!neuEinlesen && !string.IsNullOrWhiteSpace(_sofortOeffnen))
            {
                var voll = Path.GetFullPath(_sofortOeffnen);
                if (File.Exists(voll))
                {
                    var eintrag = _index.Eintraege.FirstOrDefault(
                        e => string.Equals(e.AbsoluterPfad, voll, StringComparison.OrdinalIgnoreCase));
                    ModellAnzeigen(voll, eintrag?.AssetPfad ?? Path.GetFileName(voll));
                }
                else
                {
                    HinweisSetzen($"Nicht gefunden:\n{_sofortOeffnen}");
                }
            }

            // Klassennamen im Hintergrund nachziehen; sie werden erst
            // gebraucht, wenn ein Modell ausgewaehlt ist.
            _ = Task.Run(() =>
            {
                var index = ConfigClassIndex.Erstellen(wurzeln);
                Dispatcher.Invoke(() =>
                {
                    _klassenIndex = index;

                    // Ist bereits ein Modell offen, wurde sein Klassenname
                    // noch ohne Index bestimmt und blieb leer — jetzt
                    // nachtragen.
                    if (_aktuellesModell is not null
                        && LodAuswahl.SelectedItem is LodGeometry offen)
                    {
                        InfoSetzen(_aktuellesModell, offen, _aktuellerAssetPfad);
                    }
                });
            });
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler("Bestand liess sich nicht einlesen", fehler);
            BestandText.Text = "Fehler — siehe Protokoll";
        }
        finally
        {
            KnopfNeuEinlesen.IsEnabled = true;
        }
    }

    private void NeuEinlesen_Click(object sender, RoutedEventArgs e) => BestandLaden(neuEinlesen: true);

    // -------------------------------------------------------- Auswahl

    private void Baum_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not BaumKnoten knoten) return;
        if (knoten.IstOrdner || knoten.AbsoluterPfad is null || knoten.AssetPfad is null) return;

        ModellAnzeigen(knoten.AbsoluterPfad, knoten.AssetPfad);
    }

    private void Trefferliste_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Trefferliste.SelectedItem is not TrefferAnzeige anzeige) return;
        ModellAnzeigen(anzeige.Eintrag.AbsoluterPfad, anzeige.Eintrag.AssetPfad);
    }

    private async void ModellAnzeigen(string absoluterPfad, string assetPfad)
    {
        HinweisSetzen("wird geladen…");

        try
        {
            var modell = await Task.Run(() => P3dModelReader.Read(absoluterPfad));
            var lod = modell.FeinsterSichtbarerLod;

            if (lod is null)
            {
                Viewport.Leeren();
                HinweisSetzen("Dieses Modell hat keinen sichtbaren LOD — es enthält nur "
                              + "Geometrie-, Memory- oder ähnliche technische Stufen.");
                return;
            }

            var texturen = await Task.Run(() => _texturLader.Laden(lod));

            _aktuellesModell = modell;
            _aktuellerAssetPfad = assetPfad;

            Viewport.Zeigen(modell, lod, texturen);
            HinweisVerbergen();
            Viewport.Focus();

            _lodWirdGesetzt = true;
            LodAuswahl.ItemsSource = modell.Lods;
            LodAuswahl.SelectedItem = lod;
            _lodWirdGesetzt = false;

            InfoSetzen(modell, lod, assetPfad);
            _kontext.Einstellungen.ZuletztGeoeffnet = assetPfad;
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Modell liess sich nicht laden: {assetPfad}", fehler);
            Viewport.Leeren();
            HinweisSetzen($"Lässt sich nicht anzeigen:\n{fehler.Message}");
        }
    }

    private void HinweisSetzen(string text)
    {
        ViewportHinweis.Text = text;
        HinweisRahmen.Visibility = Visibility.Visible;
    }

    private void HinweisVerbergen() => HinweisRahmen.Visibility = Visibility.Collapsed;

    // -------------------------------------------------------- Info

    private void InfoSetzen(ModelGeometry modell, LodGeometry lod, string assetPfad)
    {
        var klassen = _klassenIndex?.KlassenFuer(assetPfad) ?? [];
        var info = ModellInfo.Erzeugen(modell, lod, assetPfad, klassen, _texturLader.Fehlend);

        InfoName.Text = info.Dateiname;
        InfoPfad.Text = info.AssetPfad;
        InfoGroesse.Text = info.Groesse;
        InfoDreiecke.Text = info.Dreiecke;
        InfoVersion.Text = info.Version;
        InfoKlassen.Text = info.Klassen;

        InfoWarnung.Text = info.FehlendeTexturenText;
        WarnungRahmen.Visibility = info.HatFehlendeTexturen ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void LodAuswahl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_lodWirdGesetzt || _aktuellesModell is null) return;
        if (LodAuswahl.SelectedItem is not LodGeometry lod) return;

        if (lod.Positions.Length == 0)
        {
            Viewport.Leeren();
            HinweisSetzen($"Die Stufe „{lod.Name}“ enthält keine Geometrie.");
            return;
        }

        var texturen = await Task.Run(() => _texturLader.Laden(lod));

        Viewport.Zeigen(_aktuellesModell, lod, texturen);
        HinweisVerbergen();
        InfoSetzen(_aktuellesModell, lod, _aktuellerAssetPfad);
    }

    // -------------------------------------------------------- Ansicht

    private void Ansicht_Geaendert(object sender, RoutedEventArgs e) => AnsichtUebernehmen();

    private void AnsichtUebernehmen()
    {
        if (!_initialisiert) return;

        Viewport.BodengitterZeigen = SchalterGitter.IsChecked == true;
        Viewport.MassstabsfigurZeigen = SchalterMassstab.IsChecked == true;
        Viewport.DrahtgitterZeigen = SchalterDraht.IsChecked == true;
        Viewport.ProxysZeigen = SchalterProxys.IsChecked == true;

        _kontext.Einstellungen.BodengitterZeigen = SchalterGitter.IsChecked == true;
        _kontext.Einstellungen.MassstabsfigurZeigen = SchalterMassstab.IsChecked == true;
        _kontext.Einstellungen.DrahtgitterZeigen = SchalterDraht.IsChecked == true;
        _kontext.Einstellungen.ProxysZeigen = SchalterProxys.IsChecked == true;
    }

    private void PfadKopieren_Click(object sender, RoutedEventArgs e)
    {
        if (_aktuellerAssetPfad.Length == 0) return;

        try
        {
            Clipboard.SetText(_aktuellerAssetPfad);
        }
        catch (Exception fehler)
        {
            // Die Zwischenablage kann von einem anderen Programm belegt sein.
            _kontext.Protokoll.Fehler("Zwischenablage nicht erreichbar", fehler);
        }
    }

    // -------------------------------------------------------- Suche

    private void SuchFeld_TextChanged(object sender, TextChangedEventArgs e)
    {
        SuchHinweis.Visibility = SuchFeld.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _suchTakt.Stop();
        _suchTakt.Start();
    }

    private void SucheAusfuehren()
    {
        var text = SuchFeld.Text.Trim();

        if (text.Length == 0)
        {
            Trefferliste.Visibility = Visibility.Collapsed;
            Baum.Visibility = Visibility.Visible;
            BestandText.Text = _index is null ? string.Empty : $"{_index.Eintraege.Count:N0} Modelle";
            return;
        }

        if (_index is null) return;

        var treffer = _index.Suche(text).Select(e => new TrefferAnzeige(e)).ToList();
        Trefferliste.ItemsSource = treffer;
        Trefferliste.Visibility = Visibility.Visible;
        Baum.Visibility = Visibility.Collapsed;

        BestandText.Text = treffer.Count switch
        {
            0 => "kein Treffer",
            1 => "1 Treffer",
            _ => $"{treffer.Count:N0} Treffer",
        };

        VorschaubilderAnfordern(treffer);
    }

    // -------------------------------------------------------- Miniaturbilder

    private void VorschaubilderAnfordern(IReadOnlyList<TrefferAnzeige> treffer)
    {
        _bildWarteschlange.Clear();

        // Hoechstens 60 je Suche: mehr sieht ohnehin niemand, bevor er
        // weitertippt.
        foreach (var anzeige in treffer.Take(60))
        {
            var ausCache = _bilder.AusCache(anzeige.Eintrag.AbsoluterPfad);
            if (ausCache is not null) anzeige.Vorschau = ausCache;
            else _bildWarteschlange.Enqueue(anzeige);
        }

        BilderNachziehen();
    }

    private void BilderNachziehen()
    {
        if (_bilderLaufen || _bildWarteschlange.Count == 0) return;
        _bilderLaufen = true;

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            _bilderLaufen = false;
            if (_bildWarteschlange.Count == 0) return;

            var anzeige = _bildWarteschlange.Dequeue();
            anzeige.Vorschau = _bilder.Erzeugen(anzeige.Eintrag.AbsoluterPfad);

            BilderNachziehen();
        });
    }
}
