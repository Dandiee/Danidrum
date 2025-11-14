using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Danidrum
{
 public class BoolToPlayPauseIconConverter : IValueConverter
 {
 public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
 {
 bool isPlaying = value is bool b && b;
 return isPlaying ? PackIconKind.Pause : PackIconKind.Play;
 }

 public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
 {
 throw new NotSupportedException();
 }
 }
}
