using System.Windows;
using System.Windows.Controls;

namespace HighlandMilkBar.WPF.Controls;

public partial class LoadingIndicator : UserControl
{
    public LoadingIndicator() => InitializeComponent();

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingIndicator), new PropertyMetadata(false));

    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
}