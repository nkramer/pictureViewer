using Pictureviewer.Shell;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pictureviewer.Utilities {
    public enum DialogButtons {
        None,
        Ok,
        Cancel,
        OkCancel
    }

    // Base class for dialogs that provides common functionality:
    // - Custom/borderless window
    // - A title  
    // - proper margins & colors
    // - Ok/Cancel buttons and Escape key handling
    public class BaseDialog : Window {
        public static readonly DependencyProperty DialogTitleProperty =
            DependencyProperty.Register("DialogTitle", typeof(string), typeof(BaseDialog),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ButtonsProperty =
            DependencyProperty.Register("Buttons", typeof(DialogButtons), typeof(BaseDialog),
                new PropertyMetadata(DialogButtons.OkCancel));

        public DialogButtons Buttons {
            get { return (DialogButtons)GetValue(ButtonsProperty); }
            set { SetValue(ButtonsProperty, value); }
        }

        public string DialogTitle {
            get { return (string)GetValue(DialogTitleProperty); }
            set { SetValue(DialogTitleProperty, value); }
        }

        public BaseDialog() {
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.Background = (Brush)Application.Current.FindResource("midGray");
            this.KeyDown += BaseDialog_KeyDown;

            // Apply the template from WpfControlTemplates.xaml
            this.Template = (ControlTemplate)Application.Current.FindResource("BaseDialogTemplate");
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            // Wire up button click handlers
            if (this.Template.FindName("OkButton", this) is Button okButton) {
                okButton.Click += (s, e) => OnOk();
            }
            if (this.Template.FindName("CancelButton", this) is Button cancelButton) {
                cancelButton.Click += (s, e) => OnCancel();
            }
        }

        private void BaseDialog_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                OnCancel();
            }
        }

        protected virtual void OnOk() {
            this.DialogResult = true;
            this.Close();
        }

        protected virtual void OnCancel() {
            // We can't set DialogResult unless we called ShowDialog()
            if (!RootControl.IsShowingAllDialogs)
                this.DialogResult = false;
            this.Close();
        }
    }
}
