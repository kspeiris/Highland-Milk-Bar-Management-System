using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HighlandMilkBar.WPF.Controls;

public partial class StatusBadge : UserControl
{
    public StatusBadge() => InitializeComponent();

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty BadgeBackgroundProperty =
        DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(StatusBadge),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xEA, 0xF3, 0xFB))));
    public static readonly DependencyProperty BadgeForegroundProperty =
        DependencyProperty.Register(nameof(BadgeForeground), typeof(Brush), typeof(StatusBadge),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x14, 0x5D, 0xA0))));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Brush BadgeBackground { get => (Brush)GetValue(BadgeBackgroundProperty); set => SetValue(BadgeBackgroundProperty, value); }
    public Brush BadgeForeground { get => (Brush)GetValue(BadgeForegroundProperty); set => SetValue(BadgeForegroundProperty, value); }
}