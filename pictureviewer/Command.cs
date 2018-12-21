using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace pictureviewer
{
    public delegate void SimpleDelegate();

    public class Command : ICommand
    {
        public event SimpleDelegate Execute;
        //public event CancelEventHandler CanExecute;

        void ICommand.Execute(object parameter)
        {
            if (Execute != null)
                Execute();
        }

        bool ICommand.CanExecute(object parameter)
        {
            // not necessary for this application, + CancelEventArgs doesn't exist on Silverlight
            //CancelEventArgs args = new CancelEventArgs(false);
            //if (CanExecute != null)
            //    CanExecute(this, args);
            //return !args.Cancel;
            return true;
        }

        event EventHandler ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public Key Key = Key.None;
        public string DisplayKey;
        public ModifierKeys ModifierKeys = ModifierKeys.None;
        public bool WithOrWithoutShift = false;
        public string Text = "";
        public bool HasMenuItem = true;
        public Button Button = null; // hooks up the command to the button
    }

    public class CommandHelper
    {
        public static bool IsShiftPressed
        {
            get
            {
                return (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            }
        }
        
        private UIElement owner;
        private List<Command> commands = new List<Command>();

        public CommandHelper(UIElement owner)
        {
            this.owner = owner;
            owner.KeyDown += new KeyEventHandler(keyDown);
        }

        private void keyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is RichTextBox || e.OriginalSource is TextBox)
                return;

            foreach (Command command in commands) {
                bool shiftKeyMatches = (command.ModifierKeys & ModifierKeys.Shift) == (Keyboard.Modifiers & ModifierKeys.Shift);
                if (command.WithOrWithoutShift)
                    shiftKeyMatches = true;
                // Intentionally ignore other modifier keys
                if (command.Key == e.Key && shiftKeyMatches) {
                    (command as ICommand).Execute(null);
                    e.Handled = true;
                }
            }
        }



#if WPF
        public void AddBinding(Command command, RoutedCommand applicationCommand)
        {
            CommandBinding binding = new CommandBinding(applicationCommand);
            binding.Executed += delegate(object sender, ExecutedRoutedEventArgs e)
            {
                ((ICommand)command).Execute(null);
            };
            owner.CommandBindings.Add(binding);
        }

        public ContextMenu contextmenu;
#endif

        public void AddMenuSeparator()
        {
#if WPF
            var item = new Separator();
            contextmenu.Items.Add(item);
#endif
        }

        public void AddCommand(Command command)
        {
            commands.Add(command);

            // KeyBinding insists that ModifierKeys != 0 for alphabetic keys,
            // so we have to roll our own
            //this.CommandBindings.Add(new CommandBinding(command));
            //KeyGesture gesture = new KeyGesture(command.Key, command.ModifierKeys);
            //this.InputBindings.Add(new KeyBinding(command, gesture));

#if WPF
            if (command.HasMenuItem && contextmenu != null) {
                MenuItem item = new MenuItem();
                string text = command.Text + ShortcutText(command);
                item.Header = text;
                item.Command = command;
                contextmenu.Items.Add(item);
            }
#endif

            if (command.Button != null) {
                string text = command.Text + ShortcutText(command);
                ToolTip tooltip = new ToolTip();
                tooltip.Content = text;
                tooltip.Background = (Brush)Application.Current.Resources["menuBackground"];
                tooltip.Foreground = (Brush)Application.Current.Resources["menuForeground"];
                tooltip.BorderBrush = (Brush)Application.Current.Resources["shotclockBrush"];
                command.Button.Click += (object sender, RoutedEventArgs e) => {
                    (command as ICommand).Execute(null);
                };
#if WPF
                command.Button.ToolTip = tooltip;
                //command.Button.Command = command;
#endif
            }
        }

        private static string ShortcutText(Command command)
        {
            string text = "";
            string keyText = null;
            if (command.DisplayKey != null)
                keyText = command.DisplayKey;
            else if (command.Key != Key.None) {
                keyText = command.Key.ToString();
                if ((command.ModifierKeys & ModifierKeys.Shift) != 0)
                    keyText = "shift+" + keyText;
            }

            if (keyText != null)
                text += " (" + keyText + ")";
            return text;
        }
    }
}