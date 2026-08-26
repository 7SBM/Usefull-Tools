using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DzAssets.Preview.Shell;

/// <summary>
/// Kleinste Grundlage fuer Bindungen. Bewusst kein MVVM-Framework:
/// das Projekt kommt ohne NuGet-Pakete aus.
/// </summary>
public abstract class BeobachtbaresObjekt : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Setzen<T>(ref T feld, T wert, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(feld, wert)) return false;

        feld = wert;
        Melden(name);
        return true;
    }

    protected void Melden([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
