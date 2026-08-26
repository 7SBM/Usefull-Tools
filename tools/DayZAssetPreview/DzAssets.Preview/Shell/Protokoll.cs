using System.IO;
using System.Text;

namespace DzAssets.Preview.Shell;

/// <summary>
/// Schreibt Meldungen in eine Textdatei. Fehler beim Protokollieren
/// duerfen die Anwendung nie stoeren.
/// </summary>
public sealed class Protokoll(string datei)
{
    private readonly Lock _schloss = new();

    public string Datei { get; } = datei;

    public void Schreiben(string nachricht) => Anhaengen("INFO  ", nachricht);

    public void Fehler(string nachricht, Exception? ausnahme = null)
        => Anhaengen("FEHLER", ausnahme is null ? nachricht : $"{nachricht} :: {ausnahme}");

    /// <summary>
    /// Ein TextWriter, der ins Protokoll schreibt. Gedacht fuer
    /// Console.SetError: BisDll meldet beim Lesen von P3D-Dateien ueber
    /// Console.Error, und in einer Fensteranwendung gaebe es dafuer sonst
    /// keinen Empfaenger. Synchronisiert, weil mehrere Threads
    /// gleichzeitig Modelle lesen.
    /// </summary>
    public TextWriter AlsTextWriter() => TextWriter.Synchronized(new ProtokollSchreiber(this));

    private void Anhaengen(string art, string text)
    {
        try
        {
            var ordner = Path.GetDirectoryName(Datei);
            if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);

            lock (_schloss)
            {
                File.AppendAllText(Datei,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {art} {text}{Environment.NewLine}");
            }
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Protokollieren darf nie zum Problem werden.
        }
    }

    private sealed class ProtokollSchreiber(Protokoll ziel) : TextWriter
    {
        private readonly StringBuilder _zeile = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char zeichen)
        {
            if (zeichen == '\n')
            {
                var text = _zeile.ToString().TrimEnd('\r');
                _zeile.Clear();
                if (text.Length > 0) ziel.Anhaengen("BISDLL", text);
            }
            else if (_zeile.Length < 4096)
            {
                _zeile.Append(zeichen);
            }
        }

        public override void Write(string? wert)
        {
            if (wert is null) return;
            foreach (var zeichen in wert) Write(zeichen);
        }
    }
}
