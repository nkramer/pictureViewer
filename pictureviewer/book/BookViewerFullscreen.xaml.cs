using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

namespace pictureviewer {
    // 
    public partial class BookViewerFullscreen : UserControl, IScreen {
        private CommandHelper commands;
        private BookModel book = null;

        public BookViewerFullscreen() {
            InitializeComponent();

            book = RootControl.Instance.book;
            this.DataContext = book;

            this.commands = new CommandHelper(this);
            CreateCommands();

            this.Loaded += new RoutedEventHandler(PageDesigner_Loaded);
            this.Focusable = true;
        }

        void PageDesigner_Loaded(object sender, RoutedEventArgs e) {
            bool res = this.Focus();
        }

        private void CreateCommands() {
            Command command;

            command = new Command();
            command.Key = Key.F11;
            command.Text = "Fullscreen";
            command.Execute += delegate() {
                RootControl.Instance.PopScreen();
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Next page";
            command.Key = Key.Right;
            command.Execute += delegate() {
                NextPage(1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Previous page";
            command.Key = Key.Left;
            command.Execute += delegate() {
                NextPage(-1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Next page";
            command.Key = Key.Down;
            command.HasMenuItem = false;
            command.Execute += delegate() {
                NextPage(1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Previous page";
            command.Key = Key.Up;
            command.HasMenuItem = false;
            command.Execute += delegate() {
                NextPage(-1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Forward 1 page";
            command.Key = Key.PageDown;
            command.DisplayKey = "PageDown";
            command.Execute += delegate() {
                NextPage(1);
            };
            commands.AddCommand(command);
#if WPF
            commands.AddBinding(command, MediaCommands.FastForward);
            commands.AddBinding(command, MediaCommands.NextTrack);
#endif

            command = new Command();
            command.Text = "Backward 1 page";
            command.Key = Key.PageUp;
            command.Execute += delegate() {
                NextPage(-1);
            };
            commands.AddCommand(command);
#if WPF
            commands.AddBinding(command, MediaCommands.Rewind);
            commands.AddBinding(command, MediaCommands.PreviousTrack);
#endif
        }

        private void NextPage(int increment) {
            int i = book.Pages.IndexOf(book.SelectedPage);
            i += increment;
            i = Math.Max(0, i);
            i = Math.Min(i, book.Pages.Count - 1);
            book.SelectedPage = book.Pages[i];
        }

        void IScreen.Activate(ImageOrigin focus) {

        }

        void IScreen.Deactivate() {

        }
    }
}
