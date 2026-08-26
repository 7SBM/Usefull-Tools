using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace DzAssets.Preview.Shell;

public partial class HauptFenster : Window
{
    private readonly WerkzeugKontext _kontext;
    private readonly List<IWerkzeugModul> _module = [];
    private readonly Dictionary<IWerkzeugModul, UserControl> _ansichten = [];

    private bool _wechseltModul;

    public HauptFenster(WerkzeugKontext kontext,
                        string? sofortOeffnen = null,
                        string? suche = null,
                        string? hoehenkarte = null,
                        string? modul = null,
                        string? skript = null)
    {
        _kontext = kontext;
        InitializeComponent();

        // Weitere Werkzeuge werden hier eingehaengt. Die Shell braucht
        // dafuer keine Aenderung, nur diese Liste.
        ModuleHinzufuegen(
            new Module.AssetVorschau.AssetVorschauModul
            {
                SofortOeffnen = sofortOeffnen,
                SofortSuchen = suche,
            },
            new Module.Hoehenkarte.HoehenkarteModul
            {
                SofortOeffnen = hoehenkarte,
            },
            new Module.Debinarizer.DebinarizerModul(),
            new Module.Skripte.SkripteModul { SofortWaehlen = skript });

        // Ein ausdruecklich genanntes Modul geht vor; sonst springt eine
        // uebergebene Hoehenkarte in ihr Modul.
        if (!string.IsNullOrWhiteSpace(modul)) ModulWaehlen(modul);
        else if (!string.IsNullOrWhiteSpace(hoehenkarte)) ModulWaehlen("asc-highfield");

        StateChanged += (_, _) =>
        {
            KnopfMaximieren.Content = WindowState == WindowState.Maximized ? "" : "";
            KnopfMaximieren.ToolTip = WindowState == WindowState.Maximized
                ? "Wiederherstellen" : "Maximieren";
        };

        // Ohne diese Begrenzung deckt ein maximiertes Fenster mit eigenem
        // Chrome die Taskleiste ab.
        SourceInitialized += (_, _) => ArbeitsflaecheBegrenzen();
    }

    public void StatusSetzen(string text) => StatusText.Text = text;

    /// <summary>
    /// Waehlt ein Modul. Angenommen werden die Kennung und der Anfang des
    /// Titels — wer „--modul asc" tippt, meint zweifelsfrei die Hoehenkarte,
    /// und die frueheren Kennungen sollen weiter funktionieren.
    /// </summary>
    public void ModulWaehlen(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var gesucht = id.Trim();

        var schalter = ModulLeiste.Children.OfType<ToggleButton>()
            .FirstOrDefault(s => s.Tag is IWerkzeugModul m && Passt(m, gesucht));

        if (schalter is not null) schalter.IsChecked = true;
        else _kontext.Protokoll.Schreiben($"Modul '{gesucht}' nicht gefunden.");
    }

    private static bool Passt(IWerkzeugModul modul, string gesucht)
    {
        if (modul.Id.Equals(gesucht, StringComparison.OrdinalIgnoreCase)) return true;
        if (modul.Id.StartsWith(gesucht, StringComparison.OrdinalIgnoreCase)) return true;
        if (modul.Titel.StartsWith(gesucht, StringComparison.OrdinalIgnoreCase)) return true;

        // Die Kennungen aus der Zeit vor der Umbenennung.
        return (gesucht.ToLowerInvariant(), modul.Id) switch
        {
            ("asset-vorschau", "objekt-preview") => true,
            ("hoehenkarte", "asc-highfield") => true,
            _ => false,
        };
    }

    public void StatusRechtsSetzen(string text) => StatusRechts.Text = text;

    private void ArbeitsflaecheBegrenzen()
    {
        var quelle = PresentationSource.FromVisual(this) as HwndSource;
        var faktor = quelle?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var faktorY = quelle?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        if (faktor <= 0 || faktorY <= 0) return;

        MaxWidth = SystemParameters.WorkArea.Width + 14;
        MaxHeight = SystemParameters.WorkArea.Height + 14;
    }

    private void ModuleHinzufuegen(params IWerkzeugModul[] module)
    {
        foreach (var modul in module)
        {
            _module.Add(modul);

            var schalter = new ToggleButton
            {
                Style = (Style)FindResource("StilModulSchalter"),
                Content = modul.Symbol,
                ToolTip = $"{modul.Titel} — {modul.Beschreibung}",
                Tag = modul,
            };
            schalter.Checked += ModulSchalter_Checked;
            schalter.Unchecked += ModulSchalter_Unchecked;
            ModulLeiste.Children.Add(schalter);
        }

        if (ModulLeiste.Children.Count > 0)
            ((ToggleButton)ModulLeiste.Children[0]).IsChecked = true;
    }

    private void ModulSchalter_Checked(object sender, RoutedEventArgs e)
    {
        if (_wechseltModul) return;

        var schalter = (ToggleButton)sender;
        var modul = (IWerkzeugModul)schalter.Tag;

        _wechseltModul = true;
        try
        {
            foreach (var anderer in ModulLeiste.Children.OfType<ToggleButton>())
                if (!ReferenceEquals(anderer, schalter)) anderer.IsChecked = false;
        }
        finally
        {
            _wechseltModul = false;
        }

        if (!_ansichten.TryGetValue(modul, out var ansicht))
        {
            try
            {
                ansicht = modul.ErzeugeAnsicht(_kontext);
                _ansichten[modul] = ansicht;
            }
            catch (Exception fehler)
            {
                _kontext.Protokoll.Fehler($"Modul '{modul.Titel}' ließ sich nicht öffnen", fehler);
                StatusSetzen($"Modul '{modul.Titel}' ließ sich nicht öffnen — siehe Protokoll.");
                return;
            }
        }

        ModulInhalt.Content = ansicht;
        ModulTitel.Text = modul.Titel;
    }

    private void ModulSchalter_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_wechseltModul) return;

        // Das aktive Modul laesst sich nicht abwaehlen — es bliebe sonst
        // eine leere Arbeitsflaeche zurueck.
        var schalter = (ToggleButton)sender;
        if (ReferenceEquals(ModulInhalt.Content, _ansichten.GetValueOrDefault((IWerkzeugModul)schalter.Tag)))
            schalter.IsChecked = true;
    }

    private void Minimieren_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximieren_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Schliessen_Click(object sender, RoutedEventArgs e) => Close();
}
