using System.Windows.Controls;

namespace DzAssets.Preview.Shell;

/// <summary>
/// Ein Werkzeug in der Shell. Die Shell kennt nur diese Schnittstelle;
/// sie weiss nichts ueber P3D, PAA oder ASC, und kein Modul kennt ein
/// anderes.
/// </summary>
public interface IWerkzeugModul
{
    /// <summary>Stabile Kennung, etwa fuer Einstellungen. Nicht uebersetzt.</summary>
    string Id { get; }

    /// <summary>Beschriftung in der Modulleiste.</summary>
    string Titel { get; }

    /// <summary>Eine Glyphe aus "Segoe Fluent Icons", z. B. "".</summary>
    string Symbol { get; }

    /// <summary>Ein Satz fuer den Hinweis beim Ueberfahren mit der Maus.</summary>
    string Beschreibung { get; }

    /// <summary>
    /// Erzeugt die Ansicht. Wird von der Shell genau einmal aufgerufen —
    /// beim ersten Oeffnen des Moduls, nicht beim Programmstart. So kostet
    /// ein Modul nichts, solange es niemand benutzt.
    /// </summary>
    UserControl ErzeugeAnsicht(WerkzeugKontext kontext);
}
