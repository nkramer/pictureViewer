using Folio.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Folio.Shell;
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

public record ShortcutSection {
    public string SectionName { get; set; } = "";
    public List<ShortcutCommand> Commands { get; set; } = new();

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

public record ShortcutCommand(string KeyText, string Description);

public class KeyboardShortcutsDesignData {
    public List<ShortcutSection> Sections { get; set; }

    public KeyboardShortcutsDesignData() {
        Sections = new List<ShortcutSection> {
            new ShortcutSection {
                SectionName = "Navigation",
                Commands = new List<ShortcutCommand> {
                    new ShortcutCommand("→", "Next photo"),
                    new ShortcutCommand("←", "Previous photo"),
                    new ShortcutCommand("Home", "First photo"),
                    new ShortcutCommand("End", "Last photo"),
                    new ShortcutCommand("PgDn", "Jump forward"),
                    new ShortcutCommand("PgUp", "Jump backward")
                }
            },
            new ShortcutSection {
                SectionName = "View",
                Commands = new List<ShortcutCommand> {
                    new ShortcutCommand("F11", "Toggle fullscreen"),
                    new ShortcutCommand("Ctrl+0", "Fit to window"),
                    new ShortcutCommand("Ctrl++", "Zoom in"),
                    new ShortcutCommand("Ctrl+-", "Zoom out"),
                    new ShortcutCommand("F5", "Start slideshow"),
                    new ShortcutCommand("Esc", "Exit slideshow")
                }
            },
            new ShortcutSection {
                SectionName = "File Operations",
                Commands = new List<ShortcutCommand> {
                    new ShortcutCommand("Ctrl+O", "Open folder"),
                    new ShortcutCommand("Ctrl+S", "Save changes"),
                    new ShortcutCommand("Del", "Delete photo"),
                    new ShortcutCommand("Ctrl+C", "Copy photo"),
                    new ShortcutCommand("Ctrl+V", "Paste photo"),
                    new ShortcutCommand("F2", "Rename photo")
                }
            }
        };
    }
}
