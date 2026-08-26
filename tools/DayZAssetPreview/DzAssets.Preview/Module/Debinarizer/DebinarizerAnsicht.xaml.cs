using System.Collections.ObjectModel;
// UseWPF nimmt System.IO aus den impliziten Usings, weil Path sonst mit
// System.Windows.Shapes.Path kollidieren wuerde.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DzAssets.Formats.Models;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.Debinarizer;

public partial class DebinarizerAnsicht : UserControl
{
    /// <summary>Eine Zeile in der Dateiliste.</summary>
    public sealed class Zeile(string pfad) : BeobachtbaresObjekt
    {
        private string _zustand = "wartet";
        private string _hinweis = string.Empty;
        private Brush _randPinsel = Brushes.Gray;

        public string Pfad { get; } = pfad;
        public string Name => Path.GetFileName(Pfad);

        public string Zustand
        {
            get => _zustand;
            set => Setzen(ref _zustand, value);
        }

        public string Hinweis
        {
            get => _hinweis;
            set => Setzen(ref _hinweis, value);
        }

        public Brush RandPinsel
        {
            get => _randPinsel;
            set => Setzen(ref _randPinsel, value);
        }
    }

    private readonly WerkzeugKontext _kontext;
    private readonly ObservableCollection<Zeile> _zeilen = [];
    private readonly Dictionary<string, Zeile> _nachPfad = new(StringComparer.OrdinalIgnoreCase);

    private string? _ausgabeOrdner;
    private CancellationTokenSource? _abbruch;

    public DebinarizerAnsicht(WerkzeugKontext kontext)
    {
        _kontext = kontext;
        InitializeComponent();

        DateiListe.ItemsSource = _zeilen;
    }

    // ------------------------------------------------------------- Auswahl

    private void Dateien_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "P3D-Dateien wählen",
            Filter = "P3D-Modelle (*.p3d)|*.p3d|Alle Dateien (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        Aufnehmen(dialog.FileNames);
    }

    private async void Ordner_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Ordner mit P3D-Dateien wählen",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var ordner = dialog.FolderName;
        var mitUnter = SchalterUnterordner.IsChecked == true;

        KnopfOrdner.IsEnabled = false;
        StandText.Text = "wird durchsucht…";

        try
        {
            var gefunden = await Task.Run(
                () => P3dDebinarisierer.DateienSuchen(ordner, mitUnter));

            Aufnehmen(gefunden);
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Ordner ließ sich nicht durchsuchen: {ordner}", fehler);
            StandText.Text = "Fehler — siehe Protokoll";
        }
        finally
        {
            KnopfOrdner.IsEnabled = true;
        }
    }

    private void Aufnehmen(IEnumerable<string> pfade)
    {
        var neu = 0;

        foreach (var pfad in pfade)
        {
            if (_nachPfad.ContainsKey(pfad)) continue;

            var zeile = new Zeile(pfad) { Hinweis = Path.GetDirectoryName(pfad) ?? string.Empty };
            _nachPfad[pfad] = zeile;
            _zeilen.Add(zeile);
            neu++;
        }

        StandText.Text = $"{_zeilen.Count} Datei(en), {neu} neu aufgenommen";
        KnopfStart.IsEnabled = _zeilen.Count > 0 && _abbruch is null;
        Fortschritt.Value = 0;
    }

    private void Leeren_Click(object sender, RoutedEventArgs e)
    {
        if (_abbruch is not null) return;

        _zeilen.Clear();
        _nachPfad.Clear();
        Fortschritt.Value = 0;
        StandText.Text = "Noch nichts ausgewählt";
        KnopfStart.IsEnabled = false;
        HinweisRahmen.Visibility = Visibility.Collapsed;
    }

    private void Ziel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Ausgabeordner wählen",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        _ausgabeOrdner = dialog.FolderName;
        ZielText.Text = _ausgabeOrdner;
    }

    private void ZielZuruecksetzen_Click(object sender, RoutedEventArgs e)
    {
        _ausgabeOrdner = null;
        ZielText.Text = "neben der Quelldatei";
    }

    // ------------------------------------------------------------ Umwandeln

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_abbruch is not null || _zeilen.Count == 0) return;

        var suffix = SchalterSuffix.IsChecked == true;
        var ueberschreiben = SchalterUeberschreiben.IsChecked == true;
        var ziel = _ausgabeOrdner;

        // Ohne Suffix und ohne eigenen Ausgabeordner wird die Quelle
        // ersetzt. Das ist selten gewollt und nie umkehrbar.
        if (!suffix && string.IsNullOrEmpty(ziel) && ueberschreiben)
        {
            var antwort = MessageBox.Show(
                "Ohne Suffix und ohne Ausgabeordner werden die Quelldateien "
                + "überschrieben. Das lässt sich nicht rückgängig machen.\n\nFortfahren?",
                "Debinarizer", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (antwort != MessageBoxResult.OK) return;
        }

        var quellen = _zeilen.Select(z => z.Pfad).ToList();

        foreach (var zeile in _zeilen)
        {
            zeile.Zustand = "wartet";
            zeile.Hinweis = Path.GetDirectoryName(zeile.Pfad) ?? string.Empty;
            zeile.RandPinsel = (Brush)FindResource("PinselRand");
        }

        _abbruch = new CancellationTokenSource();
        KnopfStart.IsEnabled = false;
        KnopfAbbruch.IsEnabled = true;
        HinweisRahmen.Visibility = Visibility.Collapsed;
        Fortschritt.Value = 0;

        var fertig = 0;
        var uhr = System.Diagnostics.Stopwatch.StartNew();

        var fortschritt = new Progress<DebinBericht>(bericht =>
        {
            fertig++;
            Fortschritt.Value = 100.0 * fertig / quellen.Count;
            ZeileSetzen(bericht);
            StandText.Text = $"{fertig} von {quellen.Count}…";
        });

        try
        {
            var berichte = await Task.Run(() => P3dDebinarisierer.Umwandeln(
                quellen, ziel, suffix, ueberschreiben, fortschritt, _abbruch.Token));

            uhr.Stop();
            BerichtZusammenfassen(berichte, uhr.Elapsed);
        }
        catch (OperationCanceledException)
        {
            StandText.Text = $"abgebrochen nach {fertig} von {quellen.Count}";
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler("Umwandlung fehlgeschlagen", fehler);
            StandText.Text = "Fehler — siehe Protokoll";
        }
        finally
        {
            _abbruch?.Dispose();
            _abbruch = null;
            KnopfStart.IsEnabled = _zeilen.Count > 0;
            KnopfAbbruch.IsEnabled = false;
        }
    }

    private void ZeileSetzen(DebinBericht bericht)
    {
        if (!_nachPfad.TryGetValue(bericht.Quelle, out var zeile)) return;

        switch (bericht.Ergebnis)
        {
            case DebinErgebnis.Umgewandelt:
                zeile.Zustand = "fertig";
                zeile.Hinweis = bericht.Ziel ?? string.Empty;
                zeile.RandPinsel = (Brush)FindResource("PinselAkzent");
                break;

            case DebinErgebnis.WarSchonMlod:
                zeile.Zustand = "schon MLOD";
                zeile.Hinweis = "war nicht binarisiert — übergangen";
                zeile.RandPinsel = (Brush)FindResource("PinselTextSchwach");
                break;

            case DebinErgebnis.ZielVorhanden:
                zeile.Zustand = "vorhanden";
                zeile.Hinweis = "Ziel besteht bereits — übergangen";
                zeile.RandPinsel = (Brush)FindResource("PinselTextSchwach");
                break;

            default:
                zeile.Zustand = "Fehler";
                zeile.Hinweis = bericht.Fehler ?? "unbekannter Fehler";
                zeile.RandPinsel = (Brush)FindResource("PinselWarnung");
                break;
        }
    }

    private void BerichtZusammenfassen(IReadOnlyList<DebinBericht> berichte, TimeSpan dauer)
    {
        var fertig = berichte.Count(b => b.Ergebnis == DebinErgebnis.Umgewandelt);
        var schon = berichte.Count(b => b.Ergebnis == DebinErgebnis.WarSchonMlod);
        var vorhanden = berichte.Count(b => b.Ergebnis == DebinErgebnis.ZielVorhanden);
        var fehler = berichte.Count(b => b.Ergebnis == DebinErgebnis.Fehlgeschlagen);

        StandText.Text = $"{fertig} umgewandelt, {schon} schon MLOD, "
                         + $"{vorhanden} übergangen, {fehler} Fehler — {dauer.TotalSeconds:N1} s";

        _kontext.Protokoll.Schreiben(
            $"Debinarizer: {fertig} umgewandelt, {schon} schon MLOD, "
            + $"{vorhanden} uebergangen, {fehler} Fehler in {dauer.TotalSeconds:N1} s");

        if (fehler == 0)
        {
            HinweisRahmen.Visibility = Visibility.Collapsed;
            return;
        }

        var erste = berichte
            .Where(b => b.Ergebnis == DebinErgebnis.Fehlgeschlagen)
            .Take(4)
            .Select(b => $"{Path.GetFileName(b.Quelle)}: {b.Fehler}");

        HinweisText.Text = $"{fehler} Datei(en) schlugen fehl:{Environment.NewLine}"
                           + string.Join(Environment.NewLine, erste);
        HinweisRahmen.Visibility = Visibility.Visible;
    }

    private void Abbruch_Click(object sender, RoutedEventArgs e) => _abbruch?.Cancel();
}
