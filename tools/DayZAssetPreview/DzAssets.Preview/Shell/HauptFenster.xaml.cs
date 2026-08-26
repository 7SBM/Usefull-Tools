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
                        string? hoehenkarte = null)
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
            });

        // Wurde eine Hoehenkarte uebergeben, gleich dorthin springen.
        if (!string.IsNullOrWhiteSpace(hoehenkarte)) ModulWaehlen("hoehenkarte");

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

    /// <summary>Waehlt ein Modul ueber seine Kennung.</summary>
    public void ModulWaehlen(string id)
    {
        var schalter = ModulLeiste.Children.OfType<ToggleButton>()
            .FirstOrDefault(s => s.Tag is IWerkzeugModul m
                                 && m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (schalter is not null) schalter.IsChecked = true;
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
