namespace HighlandMilkBar.WPF.ViewModels;

public sealed class PlaceholderViewModel(string title, string description)
{
    public string Title { get; } = title;
    public string Description { get; } = description;
    public string Status => "PLANNED MODULE";
}
