using System.IO;
using BisDll.Model;

namespace DzAssets.Formats.Models;

/// <summary>Ergebnis einer einzelnen Umwandlung.</summary>
public enum DebinErgebnis
{
    Umgewandelt,
    WarSchonMlod,
    ZielVorhanden,
    Fehlgeschlagen,
}

/// <param name="Quelle">Eingelesene Datei</param>
/// <param name="Ziel">Geschriebene Datei, oder null</param>
public readonly record struct DebinBericht(
    string Quelle, string? Ziel, DebinErgebnis Ergebnis, string? Fehler);

/// <summary>
/// Wandelt binarisierte P3D (ODOL) in bearbeitbare MLOD.
///
/// Nutzt <c>Conversion.ODOL2MLOD</c> und <c>MLOD.writeToFile</c> aus
/// BisDll — dieselbe Umwandlung, die auch das Konsolenwerkzeug
/// 7SBM P3D.DeBin verwendet. Dessen Quelldatei bleibt unangetastet: sie
/// ist ein ausgeliefertes Werkzeug mit eigener Versionsnummer.
/// </summary>
public static class P3dDebinarisierer
{
    public const string MlodSuffix = "_mlod";

    /// <summary>
    /// Zielpfad zu einer Quelle. Mit <paramref name="suffix"/> entsteht
    /// aus "haus.p3d" die Datei "haus_mlod.p3d".
    /// </summary>
    public static string ZielPfad(string quelle, string? ausgabeOrdner, bool suffix)
    {
        ArgumentException.ThrowIfNullOrEmpty(quelle);

        var name = Path.GetFileNameWithoutExtension(quelle);
        if (suffix) name += MlodSuffix;

        var ordner = string.IsNullOrWhiteSpace(ausgabeOrdner)
            ? Path.GetDirectoryName(quelle) ?? string.Empty
            : ausgabeOrdner;

        return Path.Combine(ordner, name + Path.GetExtension(quelle));
    }

    /// <summary>Wandelt eine Datei um.</summary>
    public static DebinBericht Umwandeln(string quelle, string ziel, bool ueberschreiben)
    {
        ArgumentException.ThrowIfNullOrEmpty(quelle);
        ArgumentException.ThrowIfNullOrEmpty(ziel);

        try
        {
            if (!File.Exists(quelle))
                return new DebinBericht(quelle, null, DebinErgebnis.Fehlgeschlagen, "Datei nicht gefunden.");

            if (!ueberschreiben && File.Exists(ziel))
                return new DebinBericht(quelle, ziel, DebinErgebnis.ZielVorhanden, null);

            var modell = P3D.GetInstance(quelle);
            if (modell is not BisDll.Model.ODOL.ODOL odol)
                return new DebinBericht(quelle, null, DebinErgebnis.WarSchonMlod, null);

            var mlod = Conversion.ODOL2MLOD(odol);

            var ordner = Path.GetDirectoryName(ziel);
            if (!string.IsNullOrEmpty(ordner)) Directory.CreateDirectory(ordner);

            mlod.writeToFile(ziel, allowOverwriting: true);
            return new DebinBericht(quelle, ziel, DebinErgebnis.Umgewandelt, null);
        }
        catch (Exception fehler)
        {
            return new DebinBericht(quelle, null, DebinErgebnis.Fehlgeschlagen,
                $"{fehler.GetType().Name}: {fehler.Message}");
        }
    }

    /// <summary>
    /// Wandelt mehrere Dateien um, eine nach der anderen.
    ///
    /// Bewusst nicht nebenlaeufig: BisDll meldet seine Diagnose ueber
    /// <c>Console.Error</c>, und dieser Strom gilt fuer den ganzen Prozess.
    /// Zwei gleichzeitige Umwandlungen wuerden ihre Meldungen vermischen.
    /// </summary>
    public static IReadOnlyList<DebinBericht> Umwandeln(
        IReadOnlyList<string> quellen,
        string? ausgabeOrdner,
        bool suffix,
        bool ueberschreiben,
        IProgress<DebinBericht>? fortschritt = null,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(quellen);

        var berichte = new List<DebinBericht>(quellen.Count);

        foreach (var quelle in quellen)
        {
            abbruch.ThrowIfCancellationRequested();

            var bericht = Umwandeln(quelle, ZielPfad(quelle, ausgabeOrdner, suffix), ueberschreiben);
            berichte.Add(bericht);
            fortschritt?.Report(bericht);
        }

        return berichte;
    }

    /// <summary>Alle .p3d unterhalb eines Ordners.</summary>
    public static IReadOnlyList<string> DateienSuchen(string ordner, bool mitUnterordnern = true)
    {
        if (string.IsNullOrWhiteSpace(ordner) || !Directory.Exists(ordner)) return [];

        var optionen = new EnumerationOptions
        {
            RecurseSubdirectories = mitUnterordnern,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        // Bereits umgewandelte Dateien nicht erneut anfassen.
        return Directory.EnumerateFiles(ordner, "*.p3d", optionen)
            .Where(p => !Path.GetFileNameWithoutExtension(p)
                .EndsWith(MlodSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
