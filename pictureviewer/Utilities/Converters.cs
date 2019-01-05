using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;
using pictureviewer;
using Pictureviewer.Book;

namespace Pictureviewer.Utilities {

    public abstract class ValueConverter : IValueConverter {
        public abstract object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);

        public virtual object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new Exception("unsupported");
        }
    }

    public class BoolToVisibilityConverter : ValueConverter {
        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
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

    public class TemplateNameToTemplateConverter : ValueConverter {
        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value == null || value.Equals(""))
                value = PhotoPageView.GetAllTemplateNames().First();
            return PhotoPageView.GetTemplate((string)value);
        }
    }
}
