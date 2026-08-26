using System.Windows;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview;

public partial class App : Application
{
    private WerkzeugKontext? _kontext;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _kontext = WerkzeugKontext.Standard();

        // BisDll meldet beim Lesen ueber Console.Error. In einer
        // Fensteranwendung gaebe es dafuer keinen Empfaenger, also
        // umleiten — einmal, prozessweit, ins Protokoll. Kein anderer Ort
        // im Programm darf Console.SetError aufrufen: der Strom ist
        // global, und mehrere Threads lesen gleichzeitig Modelle.
        Console.SetError(_kontext.Protokoll.AlsTextWriter());
        _kontext.Protokoll.Schreiben("Anwendung gestartet.");

        DispatcherUnhandledException += (_, args) =>
        {
            _kontext.Protokoll.Fehler("Unbehandelter Fehler in der Oberflaeche", args.Exception);
            MessageBox.Show(
                $"Es ist ein Fehler aufgetreten:\n\n{args.Exception.Message}\n\n" +
                $"Einzelheiten stehen in\n{_kontext.Protokoll.Datei}",
                "DayZ Asset Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        // Ein .p3d als Aufrufargument wird sofort geoeffnet — so laesst
        // sich eine Datei direkt an das Programm uebergeben.
        var sofortOeffnen = e.Args.FirstOrDefault(
            a => a.EndsWith(".p3d", StringComparison.OrdinalIgnoreCase));

        new HauptFenster(_kontext, sofortOeffnen).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _kontext?.EinstellungenSpeichern();
        _kontext?.Protokoll.Schreiben("Anwendung beendet.");
        base.OnExit(e);
    }
}
