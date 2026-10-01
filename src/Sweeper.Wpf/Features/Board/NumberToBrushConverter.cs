using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Sweeper.Wpf.Features.Board;

// Cores clássicas, fixas de propósito (não seguem o tema).
public sealed class NumberToBrushConverter : IValueConverter
{
  private static readonly Brush[] NumberBrushes =
  [
    Brushes.Blue,
    Brushes.Green,
    Brushes.Red,
    Brushes.Navy,
    Brushes.Maroon,
    Brushes.Teal,
    Brushes.Black,
    Brushes.Gray,
  ];

  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value is int number && number is >= 1 and <= 8)
    {
      return NumberBrushes[number - 1];
    }

    return DependencyProperty.UnsetValue;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
    throw new NotSupportedException();
}
