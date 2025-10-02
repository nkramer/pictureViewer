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

    public class KeyboardShortcutsDesignData {
        public List<ShortcutSection> Sections { get; set; }

        public KeyboardShortcutsDesignData() {
            Sections = new List<ShortcutSection> {
                new ShortcutSection {
                    SectionName = "Navigation",
                    Commands = new List<ShortcutCommand> {
                        new ShortcutCommand { KeyText = "→", Description = "Next photo" },
                        new ShortcutCommand { KeyText = "←", Description = "Previous photo" },
                        new ShortcutCommand { KeyText = "Home", Description = "First photo" },
                        new ShortcutCommand { KeyText = "End", Description = "Last photo" },
                        new ShortcutCommand { KeyText = "PgDn", Description = "Jump forward" },
                        new ShortcutCommand { KeyText = "PgUp", Description = "Jump backward" }
                    }
                },
                new ShortcutSection {
                    SectionName = "View",
                    Commands = new List<ShortcutCommand> {
                        new ShortcutCommand { KeyText = "F11", Description = "Toggle fullscreen" },
                        new ShortcutCommand { KeyText = "Ctrl+0", Description = "Fit to window" },
                        new ShortcutCommand { KeyText = "Ctrl++", Description = "Zoom in" },
                        new ShortcutCommand { KeyText = "Ctrl+-", Description = "Zoom out" },
                        new ShortcutCommand { KeyText = "F5", Description = "Start slideshow" },
                        new ShortcutCommand { KeyText = "Esc", Description = "Exit slideshow" }
                    }
                },
                new ShortcutSection {
                    SectionName = "File Operations",
                    Commands = new List<ShortcutCommand> {
                        new ShortcutCommand { KeyText = "Ctrl+O", Description = "Open folder" },
                        new ShortcutCommand { KeyText = "Ctrl+S", Description = "Save changes" },
                        new ShortcutCommand { KeyText = "Del", Description = "Delete photo" },
                        new ShortcutCommand { KeyText = "Ctrl+C", Description = "Copy photo" },
                        new ShortcutCommand { KeyText = "Ctrl+V", Description = "Paste photo" },
                        new ShortcutCommand { KeyText = "F2", Description = "Rename photo" }
                    }
                }
            };
        }
    }
}
