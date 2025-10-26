using Folio.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Folio.Shell {
    public partial class KeyboardShortcutsDialog : BaseDialog {
        public KeyboardShortcutsDialog(List<ShortcutSection> sections) {
            DialogTitle = "Keyboard Shortcuts";
            InitializeComponent();
            shortcutsItemsControl.ItemsSource = sections;
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

        public List<ShortcutCommand> LeftColumnCommands {
            get {
                if (Commands == null) return new List<ShortcutCommand>();
                int halfCount = (Commands.Count + 1) / 2;
                return Commands.Take(halfCount).ToList();
            }
        }

        public List<ShortcutCommand> RightColumnCommands {
            get {
                if (Commands == null) return new List<ShortcutCommand>();
                int halfCount = (Commands.Count + 1) / 2;
                return Commands.Skip(halfCount).ToList();
            }
        }
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
