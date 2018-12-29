using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace pictureviewer {
    // Helper for implementing INotifyPropertyChanged
    public class ChangeableObject : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(String info) {
            if (PropertyChanged != null) {
                var args = new PropertyChangedEventArgs(info);
                PropertyChanged(this, args);
            }
        }
    }

    // Helper for implementing IValueConverter
    public abstract class ValueConverter : IValueConverter
    {
        public abstract object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);

        public virtual object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new Exception("unsupported");
        }
    }

    public class BoolToVisibilityConverter : ValueConverter {
        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var v = (bool)value;
            var result = (v) ? Visibility.Visible : Visibility.Collapsed;
            return result;
        }
    }

    public class BoolToScaleFlipConverter : ValueConverter {
        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            var v = (bool)value;
            double result = (v) ? -1.0 : 1.0;
            return result;
        }
    }

}
