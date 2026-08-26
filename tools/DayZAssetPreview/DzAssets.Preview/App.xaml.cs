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
                "7SBM-DayZ-Tools", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        // Ein .p3d als Aufrufargument wird sofort geoeffnet — so laesst
        // sich eine Datei direkt an das Programm uebergeben.
        var sofortOeffnen = e.Args.FirstOrDefault(
            a => a.EndsWith(".p3d", StringComparison.OrdinalIgnoreCase));

        // "--suche <text>" startet mit vorbelegtem Suchfeld.
        string? suche = null;
        var stelle = Array.FindIndex(e.Args,
            a => a.Equals("--suche", StringComparison.OrdinalIgnoreCase));
        if (stelle >= 0 && stelle + 1 < e.Args.Length) suche = e.Args[stelle + 1];

        // Eine .asc oeffnet unmittelbar das Hoehenkarten-Modul.
        var hoehenkarte = e.Args.FirstOrDefault(
            a => a.EndsWith(".asc", StringComparison.OrdinalIgnoreCase));

        // "--modul <kennung>" waehlt beim Start ein bestimmtes Werkzeug.
        string? modul = null;
        var modulStelle = Array.FindIndex(e.Args,
            a => a.Equals("--modul", StringComparison.OrdinalIgnoreCase));
        if (modulStelle >= 0 && modulStelle + 1 < e.Args.Length) modul = e.Args[modulStelle + 1];

        // "--skript <name>" waehlt beim Start ein Skript aus.
        string? skript = null;
        var skriptStelle = Array.FindIndex(e.Args,
            a => a.Equals("--skript", StringComparison.OrdinalIgnoreCase));
        if (skriptStelle >= 0 && skriptStelle + 1 < e.Args.Length) skript = e.Args[skriptStelle + 1];
        if (skript is not null) modul ??= "skripte";

        new HauptFenster(_kontext, sofortOeffnen, suche, hoehenkarte, modul, skript).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _kontext?.EinstellungenSpeichern();
        _kontext?.Protokoll.Schreiben("Anwendung beendet.");
        base.OnExit(e);
    }
}
