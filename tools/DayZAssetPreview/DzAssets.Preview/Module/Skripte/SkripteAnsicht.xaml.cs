using System.Collections.ObjectModel;
using System.Diagnostics;
// UseWPF nimmt System.IO aus den impliziten Usings, weil Path sonst mit
// System.Windows.Shapes.Path kollidieren wuerde.
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DzAssets.Formats.Skripte;
using DzAssets.Preview.Shell;

namespace DzAssets.Preview.Module.Skripte;

public partial class SkripteAnsicht : UserControl
{
    private readonly WerkzeugKontext _kontext;
    private readonly ObservableCollection<SkriptEintrag> _sichtbar = [];
    private readonly DispatcherTimer _suchTakt = new()
    {
        Interval = TimeSpan.FromMilliseconds(200),
    };

    private SkriptKatalog? _katalog;
    private SkriptEintrag? _gewaehlt;
    private CancellationTokenSource? _laufAbbruch;

    private bool _quelltextWirdGesetzt;
    private bool _geaendert;

    private readonly string? _sofortWaehlen;

    public SkripteAnsicht(WerkzeugKontext kontext, string? sofortWaehlen = null)
    {
        _kontext = kontext;
        _sofortWaehlen = sofortWaehlen;
        InitializeComponent();

        SkriptListe.ItemsSource = _sichtbar;
        _suchTakt.Tick += (_, _) => { _suchTakt.Stop(); Filtern(); };

        Loaded += (_, _) => KatalogLaden();
    }

    // ------------------------------------------------------------ Katalog

    private async void KatalogLaden()
    {
        KnopfAuffrischen.IsEnabled = false;
        ListeText.Text = "wird eingelesen…";

        var ordner = OrdnerErmitteln();

        try
        {
            _katalog = await Task.Run(() => SkriptKatalog.Erstellen(ordner));
            Filtern();

            ListeText.Text = _katalog.Eintraege.Count == 0
                ? "keine Skripte gefunden — Ordner aufnehmen"
                : $"{_katalog.Eintraege.Count} Skripte in {ordner.Count} Ordner(n)";

            _kontext.Protokoll.Schreiben($"Skripte eingelesen: {_katalog.Eintraege.Count}");

            if (!string.IsNullOrWhiteSpace(_sofortWaehlen))
            {
                SkriptListe.SelectedItem = _sichtbar.FirstOrDefault(
                    e => e.Dateiname.Equals(_sofortWaehlen, StringComparison.OrdinalIgnoreCase)
                         || e.Pfad.Equals(_sofortWaehlen, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler("Skripte ließen sich nicht einlesen", fehler);
            ListeText.Text = "Fehler — siehe Protokoll";
        }
        finally
        {
            KnopfAuffrischen.IsEnabled = true;
        }
    }

    /// <summary>
    /// Die eingestellten Ordner, ergänzt um den Skriptordner des
    /// Repositoriums, falls er neben dem Programm liegt.
    /// </summary>
    private List<string> OrdnerErmitteln()
    {
        var ordner = _kontext.Einstellungen.SkriptOrdner
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToList();

        if (ordner.Count > 0) return ordner;

        var geraten = RepositoriumsSkripte();
        if (geraten is not null)
        {
            ordner.Add(geraten);
            _kontext.Einstellungen.SkriptOrdner = ordner.ToList();
        }

        return ordner;
    }

    private static string? RepositoriumsSkripte()
    {
        var wurzel = AppContext.BaseDirectory;

        for (var i = 0; i < 10 && wurzel is not null; i++)
        {
            var kandidat = Path.Combine(wurzel, "DayZ_Helper_Scripte");
            if (Directory.Exists(kandidat)) return kandidat;
            wurzel = Path.GetDirectoryName(wurzel);
        }

        return null;
    }

    private void Filtern()
    {
        _sichtbar.Clear();
        if (_katalog is null) return;

        foreach (var eintrag in _katalog.Suche(SuchFeld.Text.Trim()))
            _sichtbar.Add(eintrag);

        if (SuchFeld.Text.Trim().Length > 0)
            ListeText.Text = $"{_sichtbar.Count} von {_katalog.Eintraege.Count}";
    }

    private void SuchFeld_TextChanged(object sender, TextChangedEventArgs e)
    {
        SuchHinweis.Visibility = SuchFeld.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _suchTakt.Stop();
        _suchTakt.Start();
    }

    private void Auffrischen_Click(object sender, RoutedEventArgs e) => KatalogLaden();

    private void Ordner_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Ordner mit Skripten aufnehmen",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var liste = _kontext.Einstellungen.SkriptOrdner.ToList();
        if (!liste.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
            liste.Add(dialog.FolderName);

        _kontext.Einstellungen.SkriptOrdner = liste;
        KatalogLaden();
    }

    // ------------------------------------------------------------- Auswahl

    private void SkriptListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkriptListe.SelectedItem is not SkriptEintrag eintrag) return;
        if (!AenderungAbfragen()) return;

        _gewaehlt = eintrag;

        KopfName.Text = eintrag.Dateiname;
        KopfPfad.Text = eintrag.Pfad;
        KopfBeschreibung.Text = eintrag.Beschreibung.Length == 0
            ? "Kein Kopfkommentar im Skript."
            : eintrag.Beschreibung;

        try
        {
            _quelltextWirdGesetzt = true;
            Quelltext.Text = File.ReadAllText(eintrag.Pfad);
            Quelltext.IsReadOnly = false;
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Skript ließ sich nicht lesen: {eintrag.Pfad}", fehler);
            Quelltext.Text = $"// Ließ sich nicht lesen: {fehler.Message}";
            Quelltext.IsReadOnly = true;
        }
        finally
        {
            _quelltextWirdGesetzt = false;
        }

        _geaendert = false;
        KnopfSpeichern.IsEnabled = false;
        KnopfOeffnen.IsEnabled = true;
        KnopfStart.IsEnabled = _laufAbbruch is null;
    }

    private void Quelltext_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_quelltextWirdGesetzt) return;

        _geaendert = true;
        KnopfSpeichern.IsEnabled = true;
    }

    /// <summary>Fragt vor dem Verwerfen ungespeicherter Änderungen nach.</summary>
    private bool AenderungAbfragen()
    {
        if (!_geaendert || _gewaehlt is null) return true;

        var antwort = MessageBox.Show(
            $"„{_gewaehlt.Dateiname}“ wurde geändert. Speichern?",
            "DayZ Asset Preview", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        switch (antwort)
        {
            case MessageBoxResult.Yes: return Speichern();
            case MessageBoxResult.No: _geaendert = false; return true;
            default: return false;
        }
    }

    private bool Speichern()
    {
        if (_gewaehlt is null) return true;

        try
        {
            // Vor dem Überschreiben eine Sicherung anlegen: die Skripte
            // sind Arbeitsstände, kein Wegwerfgut.
            var sicherung = _gewaehlt.Pfad + ".bak";
            File.Copy(_gewaehlt.Pfad, sicherung, overwrite: true);

            File.WriteAllText(_gewaehlt.Pfad, Quelltext.Text);
            _geaendert = false;
            KnopfSpeichern.IsEnabled = false;

            _kontext.Protokoll.Schreiben(
                $"Skript gespeichert: {_gewaehlt.Pfad} (Sicherung: {Path.GetFileName(sicherung)})");
            return true;
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Skript ließ sich nicht speichern: {_gewaehlt.Pfad}", fehler);
            MessageBox.Show($"Speichern misslang:\n\n{fehler.Message}",
                "DayZ Asset Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void Speichern_Click(object sender, RoutedEventArgs e) => Speichern();

    private void ImEditor_Click(object sender, RoutedEventArgs e)
    {
        if (_gewaehlt is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(_gewaehlt.Pfad) { UseShellExecute = true });
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Editor ließ sich nicht öffnen: {_gewaehlt.Pfad}", fehler);
        }
    }

    private void Neu_Click(object sender, RoutedEventArgs e)
    {
        var ordner = _kontext.Einstellungen.SkriptOrdner.FirstOrDefault()
                     ?? RepositoriumsSkripte();

        if (ordner is null || !Directory.Exists(ordner))
        {
            MessageBox.Show("Erst einen Skriptordner aufnehmen.",
                "DayZ Asset Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Neues Skript anlegen",
            InitialDirectory = ordner,
            Filter = "Python (*.py)|*.py|PowerShell (*.ps1)|*.ps1|Batch (*.bat)|*.bat",
            FileName = "neues_skript.py",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            if (!File.Exists(dialog.FileName))
                File.WriteAllText(dialog.FileName, Vorlage(dialog.FileName));

            KatalogLaden();
        }
        catch (Exception fehler)
        {
            _kontext.Protokoll.Fehler($"Skript ließ sich nicht anlegen: {dialog.FileName}", fehler);
        }
    }

    /// <summary>Gerüst mit Kopfkommentar — den liest der Katalog später aus.</summary>
    private static string Vorlage(string pfad) => SkriptEintrag.ArtAusEndung(pfad) switch
    {
        SkriptArt.Python =>
            "#!/usr/bin/env python3\n\"\"\"\n"
            + Path.GetFileName(pfad) + " — kurz beschreiben, was das Skript tut.\n\"\"\"\n\n"
            + "import sys\n\n\ndef main() -> int:\n    print(\"noch nichts zu tun\")\n    return 0\n\n\n"
            + "if __name__ == \"__main__\":\n    sys.exit(main())\n",

        SkriptArt.PowerShell =>
            "# " + Path.GetFileName(pfad) + " — kurz beschreiben, was das Skript tut.\n\n"
            + "$ErrorActionPreference = 'Stop'\n\nWrite-Output 'noch nichts zu tun'\n",

        _ =>
            ":: " + Path.GetFileName(pfad) + " — kurz beschreiben, was das Skript tut.\r\n"
            + "@echo off\r\necho noch nichts zu tun\r\n",
    };

    // --------------------------------------------------------------- Lauf

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_gewaehlt is null || _laufAbbruch is not null) return;

        if (_geaendert && !AenderungAbfragen()) return;

        var skript = _gewaehlt;
        var argumente = Zerlegen(ArgumenteFeld.Text);
        var (programm, _) = SkriptLauf.Befehl(skript, argumente);

        // Ein Skript kann alles tun, was der Anwender darf. Deshalb vorher
        // fragen und genau zeigen, was gestartet wird.
        var frage = new StringBuilder();
        frage.AppendLine($"„{skript.Dateiname}“ jetzt ausführen?");
        frage.AppendLine();
        frage.AppendLine($"Programm:      {programm}");
        frage.AppendLine($"Arbeitsordner: {skript.Ordner}");
        if (argumente.Count > 0) frage.AppendLine($"Argumente:     {string.Join(' ', argumente)}");

        if (MessageBox.Show(frage.ToString(), "Skript ausführen",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        _laufAbbruch = new CancellationTokenSource();
        KnopfStart.IsEnabled = false;
        KnopfAbbruch.IsEnabled = true;

        AusgabeAnhaengen($"── {DateTime.Now:HH:mm:ss}  {programm} {skript.Dateiname} ──");

        var fortschritt = new Progress<AusgabeZeile>(z =>
            AusgabeAnhaengen(z.IstFehler ? "! " + z.Text : z.Text));

        try
        {
            var code = await SkriptLauf.AusfuehrenAsync(
                skript, fortschritt, argumente, skript.Ordner, _laufAbbruch.Token);

            AusgabeAnhaengen(code == 0
                ? $"── fertig, Rückgabewert {code} ──"
                : $"── beendet mit Rückgabewert {code} ──");

            _kontext.Protokoll.Schreiben($"Skript gelaufen: {skript.Dateiname} -> {code}");
        }
        catch (OperationCanceledException)
        {
            AusgabeAnhaengen("── abgebrochen ──");
        }
        catch (Exception fehler)
        {
            AusgabeAnhaengen($"── Fehler: {fehler.Message} ──");
            _kontext.Protokoll.Fehler($"Skript ließ sich nicht ausführen: {skript.Pfad}", fehler);
        }
        finally
        {
            _laufAbbruch?.Dispose();
            _laufAbbruch = null;
            KnopfStart.IsEnabled = _gewaehlt is not null;
            KnopfAbbruch.IsEnabled = false;
        }
    }

    private void Abbruch_Click(object sender, RoutedEventArgs e) => _laufAbbruch?.Cancel();

    private void Leeren_Click(object sender, RoutedEventArgs e) => Ausgabe.Clear();

    private void AusgabeAnhaengen(string zeile)
    {
        Ausgabe.AppendText(zeile + Environment.NewLine);
        Ausgabe.ScrollToEnd();
    }

    /// <summary>
    /// Zerlegt die Argumentzeile an Leerzeichen, achtet dabei auf
    /// Anführungszeichen — Pfade mit Leerzeichen sind hier der Regelfall.
    /// </summary>
    public static List<string> Zerlegen(string? zeile)
    {
        var ergebnis = new List<string>();
        if (string.IsNullOrWhiteSpace(zeile)) return ergebnis;

        var sammler = new StringBuilder();
        var inAnfuehrung = false;

        foreach (var zeichen in zeile)
        {
            if (zeichen == '"')
            {
                inAnfuehrung = !inAnfuehrung;
                continue;
            }

            if (char.IsWhiteSpace(zeichen) && !inAnfuehrung)
            {
                if (sammler.Length > 0) { ergebnis.Add(sammler.ToString()); sammler.Clear(); }
                continue;
            }

            sammler.Append(zeichen);
        }

        if (sammler.Length > 0) ergebnis.Add(sammler.ToString());
        return ergebnis;
    }
}
