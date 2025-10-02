using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Pictureviewer.Shell {
    public partial class KeyboardShortcutsWindow : Window {
        public KeyboardShortcutsWindow(List<ShortcutSection> sections) {
            InitializeComponent();
            shortcutsItemsControl.ItemsSource = sections;
            this.KeyDown += KeyboardShortcutsWindow_KeyDown;
        }

        private void KeyboardShortcutsWindow_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                this.Close();
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.OriginalSource == this) {
                this.Close();
            }
        }
    }

    public class ShortcutSection {
        public string SectionName { get; set; }
        public List<ShortcutCommand> Commands { get; set; }
    }

    public class ShortcutCommand {
        public string KeyText { get; set; }
        public string Description { get; set; }
    }
}
