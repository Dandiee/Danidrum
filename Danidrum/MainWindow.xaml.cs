using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Danidrum;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();
    }
}

public sealed class BoolToVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (bool)value ? Visibility.Visible : Visibility.Collapsed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => false;
}

public sealed class BoolNegationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value != null && value is bool b) return !b;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value != null && value is bool b) return !b;

        return false;
    }
}