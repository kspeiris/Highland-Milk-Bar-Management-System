using System.Windows;
using System.Windows.Controls;

namespace HighlandMilkBar.WPF.Controls;

public partial class SearchBox : UserControl
{
    public SearchBox() => InitializeComponent();

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(SearchBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
}