using System;
using System.Windows;

namespace Folio.Tests {
    /// <summary>
    /// Helper class to ensure WPF Application is initialized properly for tests.
    /// WPF only allows one Application instance per AppDomain.
    /// </summary>
    public static class WpfTestHelper {
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// Ensures that a WPF Application exists and is initialized with required resources.
        /// This method is thread-safe and will only create one Application per AppDomain.
        /// </summary>
        public static void EnsureApplicationInitialized() {
            lock (_lock) {
                if (_isInitialized) {
                    return;
                }

                if (Application.Current == null) {
                    new Application();
                }

                var app = Application.Current;

                // Load all required resource dictionaries
                var miscResources = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/MiscResources.xaml") };
                var templates = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/Templates_875x1125.xaml") };
                var samples = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/Templates_Samples.xaml") };
                var wpfTemplates = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/WpfControlTemplates.xaml") };

                app.Resources.MergedDictionaries.Add(miscResources);
                app.Resources.MergedDictionaries.Add(templates);
                app.Resources.MergedDictionaries.Add(samples);
                app.Resources.MergedDictionaries.Add(wpfTemplates);

                _isInitialized = true;
            }
        }
    }
}
