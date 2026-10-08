using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LawCaseManagement.Wpf.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns pill-shaped chip background color according to case/task status.
    /// Open -> SuccessSoftBrush
    /// In Progress / Pending -> WarningSoftBrush
    /// Closed / Archived / Completed -> ArchivedSoftBrush
    /// Conflict -> ErrorSoftBrush
    /// </summary>
    public class StatusToChipBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? string.Empty;
            var app = Application.Current;
            if (app == null) return Brushes.Transparent;

            return status.ToLowerInvariant() switch
            {
                "open" => app.TryFindResource("SuccessSoftBrush") ?? Brushes.Transparent,
                "in progress" or "pending" => app.TryFindResource("WarningSoftBrush") ?? Brushes.Transparent,
                "closed" or "archived" or "completed" => app.TryFindResource("ArchivedSoftBrush") ?? Brushes.Transparent,
                "conflict" => app.TryFindResource("ErrorSoftBrush") ?? Brushes.Transparent,
                _ => app.TryFindResource("SurfaceAltBrush") ?? Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns pill-shaped chip text / border color according to case/task status.
    /// </summary>
    public class StatusToChipForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? string.Empty;
            var app = Application.Current;
            if (app == null) return Brushes.Black;

            return status.ToLowerInvariant() switch
            {
                "open" => app.TryFindResource("SuccessBrush") ?? Brushes.Green,
                "in progress" or "pending" => app.TryFindResource("WarningBrush") ?? Brushes.Goldenrod,
                "closed" or "archived" or "completed" => app.TryFindResource("ArchivedBrush") ?? Brushes.Gray,
                "conflict" => app.TryFindResource("ErrorBrush") ?? Brushes.Red,
                _ => app.TryFindResource("TextSecondaryBrush") ?? Brushes.DarkSlateGray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
