using System.Collections.ObjectModel;
using System.Globalization;
// UseWPF nimmt System.IO aus den impliziten Usings, weil Path sonst mit
// System.Windows.Shapes.Path kollidieren wuerde.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DzAssets.Formats.Hoehen;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.Hoehenkarte;

public partial class HoehenkarteAnsicht : UserControl
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Ein Eintrag in der Kartenliste.</summary>
    public sealed record KartenEintrag(string Pfad, string Name, string Beschreibung);

    private readonly WerkzeugKontext _kontext;
    private readonly ObservableCollection<KartenEintrag> _karten = [];

    private AscRaster? _raster;
    private string _aktuellerPfad = string.Empty;
    private bool _initialisiert;
    private bool _laedt;

    private readonly string? _sofortOeffnen;

    public HoehenkarteAnsicht(WerkzeugKontext kontext, string? sofortOeffnen = null)
    {
        _kontext = kontext;
        _sofortOeffnen = sofortOeffnen;
        InitializeComponent();

        KartenListe.ItemsSource = _karten;

        SkalaAuswahl.ItemsSource = new[] { "Gelände", "Graustufen", "Verlauf" };
        SkalaAuswahl.SelectedIndex = 0;

        // Netzfeinheit getrennt von der Texturauflösung: 512² sind bereits
        // über eine halbe Million Dreiecke, während eine Textur von 2048²
        // praktisch nichts kostet.
        NetzAuswahl.ItemsSource = new[]
        {
            "Grob (128)", "Mittel (256)", "Fein (512)", "Sehr fein (1024)",
        };
        NetzAuswahl.SelectedIndex = 2;

        _initialisiert = true;

        Loaded += (_, _) => ZuletztLaden();
    }

    private int NetzKante => NetzAuswahl.SelectedIndex switch
    {
        0 => 128,
        1 => 256,
        3 => 1024,
        _ => 512,
    };

    private Farbskala Skala => SkalaAuswahl.SelectedIndex switch
    {
        1 => Farbskala.Graustufen,
        2 => Farbskala.Verlauf,
        _ => Farbskala.Gelaende,
    };

    // ------------------------------------------------------------ Auswahl

    private void ZuletztLaden()
    {
        foreach (var pfad in _kontext.Einstellungen.ZuletztHoehenkarten.ToList())
        {
            if (!File.Exists(pfad)) continue;
            EintragAufnehmen(pfad);
        }

        if (!string.IsNullOrWhiteSpace(_sofortOeffnen) && File.Exists(_sofortOeffnen))
        {
            var voll = Path.GetFullPath(_sofortOeffnen);
            EintragAufnehmen(voll);
            KartenListe.SelectedItem = _karten.FirstOrDefault(
                k => string.Equals(k.Pfad, voll, StringComparison.OrdinalIgnoreCase));
        }

        ListeText.Text = _karten.Count == 0
            ? "Noch keine Karte geladen"
            : $"{_karten.Count} Karte(n)";
    }

    private void EintragAufnehmen(string pfad)
    {
        if (_karten.Any(k => string.Equals(k.Pfad, pfad, StringComparison.OrdinalIgnoreCase)))
            return;

        string beschreibung;
        try
        {
            var groesse = new FileInfo(pfad).Length;
            beschreibung = $"{groesse / (1024.0 * 1024.0):N1} MB · {Ordnername(pfad)}";
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            beschreibung = Ordnername(pfad);
        }

        _karten.Add(new KartenEintrag(pfad, Path.GetFileNameWithoutExtension(pfad), beschreibung));
    }

    private static string Ordnername(string pfad)
    {
        var ordner = Path.GetDirectoryName(pfad);
        return string.IsNullOrEmpty(ordner) ? string.Empty : ordner;
    }

    private void Datei_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "ASC-Höhenkarte öffnen",
            Filter = "Esri-ASCII-Grid (*.asc)|*.asc|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        EintragAufnehmen(dialog.FileName);
        KartenListe.SelectedItem = _karten.FirstOrDefault(
            k => string.Equals(k.Pfad, dialog.FileName, StringComparison.OrdinalIgnoreCase));
    }

    private async void Ordner_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Ordner nach ASC-Karten durchsuchen",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var ordner = dialog.FolderName;
        KnopfOrdner.IsEnabled = false;
        ListeText.Text = "wird durchsucht…";

        try
        {
            var gefunden = await Task.Run(() =>
            {
                var optionen = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                };

                return Directory.EnumerateFiles(ordner, "*.asc", optionen).Take(500).ToList();
            });

            foreach (var pfad in gefunden) EintragAufnehmen(pfad);

            ListeText.Text = gefunden.Count == 0
                ? "keine ASC-Datei gefunden"
                : $"{_karten.Count} Karte(n), {gefunden.Count} neu gefunden";
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Ordner ließ sich nicht durchsuchen: {ordner}", fehler);
            ListeText.Text = "Fehler — siehe Protokoll";
        }
        finally
        {
            KnopfOrdner.IsEnabled = true;
        }
    }

    private void KartenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KartenListe.SelectedItem is not KartenEintrag eintrag) return;
        KarteLaden(eintrag.Pfad);
    }

    // ------------------------------------------------------------- Laden

    private async void KarteLaden(string pfad)
    {
        if (_laedt) return;
        _laedt = true;

        HinweisSetzen($"{Path.GetFileName(pfad)} wird gelesen…", mitFortschritt: true);

        try
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();

            var fortschritt = new Progress<int>(zeile =>
            {
                if (_raster is not null) return;
                Fortschritt.IsIndeterminate = true;
            });

            var raster = await Task.Run(() => AscRaster.Laden(pfad, fortschritt));
            uhr.Stop();

            _raster = raster;
            _aktuellerPfad = pfad;

            _kontext.Protokoll.Schreiben(
                $"Höhenkarte gelesen: {Path.GetFileName(pfad)} "
                + $"{raster.Spalten}x{raster.Zeilen} in {uhr.ElapsedMilliseconds} ms");

            ZuletztMerken(pfad);
            InfoSetzen(raster, pfad);
            await DarstellungAufbauen();
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Höhenkarte ließ sich nicht lesen: {pfad}", fehler);
            _raster = null;
            Viewport.Leeren();
            HinweisSetzen($"Lässt sich nicht lesen:\n{fehler.Message}");
        }
        finally
        {
            _laedt = false;
        }
    }

    private void ZuletztMerken(string pfad)
    {
        var liste = _kontext.Einstellungen.ZuletztHoehenkarten;
        liste.RemoveAll(p => string.Equals(p, pfad, StringComparison.OrdinalIgnoreCase));
        liste.Insert(0, pfad);
        while (liste.Count > 15) liste.RemoveAt(liste.Count - 1);
    }

    /// <summary>
    /// Baut Netz und Textur neu. Die Textur wird feiner gerechnet als das
    /// Netz — das Gelände sieht dadurch deutlich feiner aus, als es
    /// vernetzt ist, ohne dass die Dreiecksanzahl steigt.
    /// </summary>
    private async Task DarstellungAufbauen()
    {
        if (_raster is null) return;

        var raster = _raster;
        var netzKante = NetzKante;
        var skala = Skala;
        var schummerung = SchummerungRegler.Value;

        HinweisSetzen("Gelände wird aufgebaut…", mitFortschritt: true);

        try
        {
            var (netzRaster, texturBild, texturBreite, texturHoehe) = await Task.Run(() =>
            {
                var fuersNetz = Reliefbild.Verkleinern(raster, netzKante);

                // Textur höchstens 2048 — darüber bringt es auf dem
                // Bildschirm nichts mehr und kostet 16 MB je Verdopplung.
                var fuerTextur = Reliefbild.Verkleinern(raster, 2048);
                var bild = Reliefbild.Einfaerben(fuerTextur, skala, raster.Zellgroesse, schummerung);

                return (fuersNetz, bild, fuerTextur.Spalten, fuerTextur.Zeilen);
            });

            var textur = BitmapSource.Create(texturBreite, texturHoehe, 96, 96,
                PixelFormats.Bgra32, null, texturBild, texturBreite * 4);
            textur.Freeze();

            Viewport.Zeigen(netzRaster, textur, raster.Zellgroesse, UeberhoehungRegler.Value);
            Viewport.DrahtgitterZeigen = SchalterDraht.IsChecked == true;

            var dreiecke = (netzRaster.Spalten - 1) * (netzRaster.Zeilen - 1) * 2;
            NetzHinweis.Text =
                $"{netzRaster.Spalten} × {netzRaster.Zeilen} Punkte, "
                + $"{dreiecke:N0} Dreiecke · Textur {texturBreite} × {texturHoehe}";

            HinweisVerbergen();
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler("Geländeansicht ließ sich nicht aufbauen", fehler);
            HinweisSetzen($"Ansicht ließ sich nicht aufbauen:\n{fehler.Message}");
        }
    }

    // -------------------------------------------------------------- Info

    private void InfoSetzen(AscRaster raster, string pfad)
    {
        InfoName.Text = Path.GetFileNameWithoutExtension(pfad);
        InfoPfad.Text = pfad;

        InfoRaster.Text = string.Format(Deutsch, "{0:N0} × {1:N0} Zellen à {2:N2} m",
            raster.Spalten, raster.Zeilen, raster.Zellgroesse);

        InfoAusdehnung.Text = string.Format(Deutsch, "{0:N0} × {1:N0} m  ({2:N2} × {3:N2} km)",
            raster.BreiteMeter, raster.HoeheMeter,
            raster.BreiteMeter / 1000, raster.HoeheMeter / 1000);

        InfoHoehe.Text = string.Format(Deutsch, "{0:N1} m bis {1:N1} m  (Δ {2:N1} m)",
            raster.Min, raster.Max, raster.Max - raster.Min);

        InfoLuecken.Text = raster.NoDataAnzahl == 0
            ? "keine"
            : string.Format(Deutsch, "{0:N0} Zellen ({1:N2} %)",
                raster.NoDataAnzahl,
                100.0 * raster.NoDataAnzahl / raster.Werte.Length);
    }

    private void HinweisSetzen(string text, bool mitFortschritt = false)
    {
        Hinweis.Text = text;
        HinweisRahmen.Visibility = Visibility.Visible;
        Fortschritt.Visibility = mitFortschritt ? Visibility.Visible : Visibility.Collapsed;
        Fortschritt.IsIndeterminate = mitFortschritt;
    }

    private void HinweisVerbergen()
    {
        HinweisRahmen.Visibility = Visibility.Collapsed;
        Fortschritt.IsIndeterminate = false;
    }

    // ------------------------------------------------------- Einstellungen

    private async void Darstellung_Geaendert(object sender, RoutedEventArgs e)
    {
        if (!_initialisiert) return;

        SchummerungText.Text = $"{SchummerungRegler.Value * 100:N0} %";
        if (_raster is null || _laedt) return;

        await DarstellungAufbauen();
    }

    private void Ueberhoehung_Geaendert(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialisiert) return;

        UeberhoehungText.Text = string.Format(Deutsch, "{0:N1} ×", UeberhoehungRegler.Value);
        if (_raster is null) return;

        // Nur das Netz neu bauen, die Textur bleibt — das geht sofort.
        Viewport.UeberhoehungSetzen(UeberhoehungRegler.Value);
    }

    private void Draht_Geaendert(object sender, RoutedEventArgs e)
    {
        if (!_initialisiert) return;
        Viewport.DrahtgitterZeigen = SchalterDraht.IsChecked == true;
    }

    private void Einrahmen_Click(object sender, RoutedEventArgs e)
    {
        Viewport.Einrahmen();
        Viewport.Focus();
    }
}
