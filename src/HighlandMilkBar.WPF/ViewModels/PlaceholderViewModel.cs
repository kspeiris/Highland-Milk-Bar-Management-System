namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>
/// A professional page scaffold for modules that are not implemented yet. It renders a
/// page header, a (disabled) toolbar and a clear empty state so no button silently does nothing.
/// </summary>
public sealed class PlaceholderViewModel : ObservableObject
{
    public PlaceholderViewModel(string title, string subtitle, string description, string plannedStep, string glyph)
    {
        Title = title;
        Subtitle = subtitle;
        Description = description;
        PlannedStep = plannedStep;
        Glyph = glyph;
    }

    public string Title { get; }
    public string Subtitle { get; }
    public string Description { get; }
    public string PlannedStep { get; }
    public string Glyph { get; }
    public string Status => "PLANNED MODULE";
}