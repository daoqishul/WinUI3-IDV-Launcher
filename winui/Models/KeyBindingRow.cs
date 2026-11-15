using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IdvLauncher.Models;

public sealed class KeyBindingRow : INotifyPropertyChanged
{
    public int Op { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Group { get; init; } = "";
    public int? KeyCode { get; set; }

    private string _keyDisplay = "未设置";
    public string KeyDisplay
    {
        get => _keyDisplay;
        set
        {
            if (_keyDisplay == value) return;
            _keyDisplay = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
        }
    }

    public string Display => $"{Name}    {KeyDisplay}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
