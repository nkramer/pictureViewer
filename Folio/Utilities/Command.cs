using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Folio.Utilities; 
public delegate void SimpleDelegate();

public class Command : ICommand {
    public event SimpleDelegate? Execute;
    //public event CancelEventHandler CanExecute;

    // Set to true if this command should record a snapshot before executing (for undo/redo).
    // The actual snapshot callback is set on the CommandHelper.
    public bool ShouldRecordSnapshot = false;

    // Internal callback to record a snapshot. Set by CommandHelper.
    internal Action? RecordSnapshot = null;

    void ICommand.Execute(object? parameter) {
        // Record snapshot before executing, if callback is set
        if (RecordSnapshot != null)
            RecordSnapshot();

        if (Execute != null)
            Execute();
    }

    bool ICommand.CanExecute(object? parameter) {
        // not necessary for this application, + CancelEventArgs doesn't exist on Silverlight
        //CancelEventArgs args = new CancelEventArgs(false);
        //if (CanExecute != null)
        //    CanExecute(this, args);
        //return !args.Cancel;
        return true;
    }

    event EventHandler? ICommand.CanExecuteChanged {
        add { }
        remove { }
    }

    public Key Key = Key.None;
    public string? DisplayKey;
    public ModifierKeys ModifierKeys = ModifierKeys.None;
    public bool WithOrWithoutShift = false;
    public string Text = "";
    public bool HasMenuItem = true;
    public Button? Button = null; // hooks up the command to the button
}

public class CommandHelper {
    public static bool IsShiftPressed {
        get {
            return (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        }
    }

    private UIElement owner;
    private List<Command> commands = new List<Command>();
    private List<int> separatorIndices = new List<int>(); // Track where separators should be

    // Callback to record a snapshot before executing commands with ShouldRecordSnapshot = true.
    // Set this once to enable undo/redo for all commands that need it.
    public Action? RecordSnapshot = null;

    public CommandHelper(UIElement owner) : this(owner, false) {
    }

    public CommandHelper(UIElement owner, bool preview) {
        this.owner = owner;
        if (preview) {
            owner.PreviewKeyDown += new KeyEventHandler(keyDown);
        } else {
            owner.KeyDown += new KeyEventHandler(keyDown);
        }
    }

    private void keyDown(object? sender, KeyEventArgs e) {
        bool ctrlRequired = e.OriginalSource is RichTextBox || e.OriginalSource is TextBox;

        //if ((e.OriginalSource is RichTextBox || e.OriginalSource is TextBox)
        //    && (Keyboard.ModifierKeys & ModifierKeys.Control) == 0)
        //    return;

        foreach (Command command in commands) {
            bool ctrlKeyMatches = (ctrlRequired)
                ? (Keyboard.Modifiers & ModifierKeys.Control) != 0
                : true;
            bool shiftKeyMatches = (command.ModifierKeys & ModifierKeys.Shift) == (Keyboard.Modifiers & ModifierKeys.Shift);
            if (command.WithOrWithoutShift)
                shiftKeyMatches = true;
            // Intentionally ignore other modifier keys
            if (command.Key == e.Key && ctrlKeyMatches && shiftKeyMatches) {
                (command as ICommand).Execute(null);
                e.Handled = true;
            }
        }
    }



#if WPF
    public void AddBinding(Command command, RoutedCommand applicationCommand) {
        CommandBinding binding = new CommandBinding(applicationCommand);
        binding.Executed += delegate (object? sender, ExecutedRoutedEventArgs e) {
            ((ICommand)command).Execute(null);
        };
        owner.CommandBindings.Add(binding);
    }

    public ContextMenu? contextmenu;
#endif

    public void AddMenuSeparator() {
#if WPF
        // Track that a separator should appear before the next command
        separatorIndices.Add(commands.Count);
        // The separator control has funny spacing on the left and I can't figure out why. 
        var rectangle = new System.Windows.Shapes.Rectangle {
            Height = 1,
            Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 4)
        };
        contextmenu!.Items.Add(rectangle);
#endif
    }

    public void AddCommand(Command command) {
        commands.Add(command);

        // Wire up the snapshot callback if this command should record snapshots
        if (command.ShouldRecordSnapshot && RecordSnapshot != null) {
            command.RecordSnapshot = RecordSnapshot;
        }

        // KeyBinding insists that ModifierKeys != 0 for alphabetic keys,
        // so we have to roll our own
        //this.CommandBindings.Add(new CommandBinding(command));
        //KeyGesture gesture = new KeyGesture(command.Key, command.ModifierKeys);
        //this.InputBindings.Add(new KeyBinding(command, gesture));

        AddToContextMenuIfNeeded(command);

        if (command.Button != null) {
            string text = command.Text + ShortcutText(command);
            ToolTip tooltip = new ToolTip();
            tooltip.Content = text;
            tooltip.Background = (Brush)Application.Current.Resources["menuBackground"];
            tooltip.Foreground = (Brush)Application.Current.Resources["menuForeground"];
            tooltip.BorderBrush = (Brush)Application.Current.Resources["shotclockBrush"];
            command.Button.Click += (object? sender, RoutedEventArgs e) => {
                (command as ICommand).Execute(null);
            };
#if WPF
            command.Button.ToolTip = tooltip;
            //command.Button.Command = command;
#endif
        }
    }

    private void AddToContextMenuIfNeeded(Command command) {
#if WPF
        if (command.HasMenuItem && contextmenu != null) {
            MenuItem item = new MenuItem();
            string text = command.Text + ShortcutText(command);
            item.Header = text;
            item.Command = command;
            contextmenu.Items.Add(item);
        }
#endif
    }

    public void MergeMenus(CommandHelper parent) {
        AddMenuSeparator();
        int baseIndex = commands.Count;
        for (int i = 0; i < parent.commands.Count; i++) {
            // Add separator if parent had one at this position
            if (parent.separatorIndices.Contains(i)) {
                AddMenuSeparator();
            }
            AddToContextMenuIfNeeded(parent.commands[i]);
        }
    }

    private static string ShortcutText(Command command) {
        string text = "";
        string keyText = GetKeyText(command);

        if (keyText != "")
            text += " (" + keyText + ")";
        return text;
    }

    public static string GetKeyText(Command command) {
        string keyText = "";
        if (command.DisplayKey != null)
            keyText = command.DisplayKey;
        else if (command.Key != Key.None) {
            keyText = command.Key.ToString();
            if (command.Key >= Key.D0 && command.Key <= Key.D9)
                keyText = keyText.Substring(1);
            if ((command.ModifierKeys & ModifierKeys.Shift) != 0)
                keyText = "Shift+" + keyText;
            if ((command.ModifierKeys & ModifierKeys.Control) != 0)
                keyText = "Ctrl+" + keyText;
            if ((command.ModifierKeys & ModifierKeys.Alt) != 0)
                keyText = "Alt+" + keyText;
        }
        return keyText;
    }

    public List<Command> GetCommands() {
        return commands;
    }
}