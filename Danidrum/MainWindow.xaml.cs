using System.Globalization;
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