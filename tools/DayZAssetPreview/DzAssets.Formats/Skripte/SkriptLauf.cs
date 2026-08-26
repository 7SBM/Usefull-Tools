using System.Diagnostics;
using System.IO;

namespace DzAssets.Formats.Skripte;

/// <summary>Eine Ausgabezeile eines laufenden Skripts.</summary>
/// <param name="Text">die Zeile</param>
/// <param name="IstFehler">wahr, wenn sie von der Fehlerausgabe stammt</param>
public readonly record struct AusgabeZeile(string Text, bool IstFehler);

/// <summary>
/// Startet ein Hilfsskript und reicht dessen Ausgabe zeilenweise durch.
///
/// Bewusst kein Shell-Aufruf mit zusammengesetzter Befehlszeile: Pfad und
/// Argumente gehen als getrennte Werte an den Aufruf, damit Leerzeichen
/// und Sonderzeichen in Pfaden nichts anrichten koennen.
/// </summary>
public static class SkriptLauf
{
    /// <summary>Wie der Aufruf fuer eine Skriptart aussieht.</summary>
    public static (string Programm, IReadOnlyList<string> Argumente) Befehl(
        SkriptEintrag skript, IReadOnlyList<string>? eigeneArgumente = null)
    {
        ArgumentNullException.ThrowIfNull(skript);
        var extra = eigeneArgumente ?? [];

        return skript.Art switch
        {
            SkriptArt.Python => ("py", new[] { skript.Pfad }.Concat(extra).ToArray()),

            // -NoProfile, damit das Profil des Anwenders den Lauf nicht
            // beeinflusst; -ExecutionPolicy Bypass, weil die eigenen
            // Skripte nicht signiert sind.
            SkriptArt.PowerShell => ("powershell", new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", skript.Pfad,
            }.Concat(extra).ToArray()),

            SkriptArt.Batch => ("cmd.exe", new[] { "/c", skript.Pfad }.Concat(extra).ToArray()),

            _ => (skript.Pfad, extra.ToArray()),
        };
    }

    /// <summary>
    /// Fuehrt das Skript aus und meldet jede Ausgabezeile.
    /// Liefert den Rueckgabewert des Programms.
    /// </summary>
    /// <param name="arbeitsordner">
    /// Ordner, in dem das Skript laeuft. Viele der Skripte erwarten, im
    /// Zielverzeichnis zu stehen; ohne Angabe ist es der Skriptordner.
    /// </param>
    public static async Task<int> AusfuehrenAsync(
        SkriptEintrag skript,
        IProgress<AusgabeZeile> ausgabe,
        IReadOnlyList<string>? argumente = null,
        string? arbeitsordner = null,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(skript);
        ArgumentNullException.ThrowIfNull(ausgabe);

        if (!File.Exists(skript.Pfad))
            throw new FileNotFoundException("Skript nicht gefunden.", skript.Pfad);

        var (programm, args) = Befehl(skript, argumente);

        var start = new ProcessStartInfo
        {
            FileName = programm,
            WorkingDirectory = arbeitsordner is { Length: > 0 } && Directory.Exists(arbeitsordner)
                ? arbeitsordner
                : skript.Ordner,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args) start.ArgumentList.Add(a);

        using var prozess = new Process { StartInfo = start, EnableRaisingEvents = true };

        prozess.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) ausgabe.Report(new AusgabeZeile(e.Data, false));
        };
        prozess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) ausgabe.Report(new AusgabeZeile(e.Data, true));
        };

        try
        {
            prozess.Start();
        }
        catch (Exception fehler)
        {
            throw new InvalidOperationException(
                $"„{programm}“ liess sich nicht starten. Ist es installiert und im Pfad?", fehler);
        }

        prozess.BeginOutputReadLine();
        prozess.BeginErrorReadLine();

        // Die Eingabe schliessen: ein Skript, das auf eine Eingabe wartet,
        // haengt sonst ohne Aussicht auf Fortschritt.
        try { prozess.StandardInput.Close(); } catch (IOException) { }

        try
        {
            await prozess.WaitForExitAsync(abbruch).ConfigureAwait(false);
            return prozess.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!prozess.HasExited) prozess.Kill(entireProcessTree: true);
            }
            catch (Exception fehler) when (fehler is InvalidOperationException or NotSupportedException)
            {
                // Der Prozess war schon weg.
            }

            throw;
        }
    }
}
