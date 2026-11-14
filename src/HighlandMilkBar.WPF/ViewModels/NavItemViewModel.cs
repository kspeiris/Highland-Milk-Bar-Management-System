namespace HighlandMilkBar.WPF.ViewModels;

public sealed class NavItemViewModel(string title, string glyph, string key) : ObservableObject
{
    private bool _isSelected;
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    public string Key { get; } = key;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}
