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

    /// <summary>
    /// Base class for dialogs that provides common functionality:
    /// - 32px padding around dialog contents (via ControlTemplate in WpfControlTemplates.xaml)
    /// - Title property using TitleTextBlockStyle
    /// - Button bar at bottom (OkCancel, Ok, Cancel, or None)
    /// - Escape key cancels the dialog
    /// - WindowStyle="None"
    /// - ResizeMode="NoResize"
    /// - WindowStartupLocation="CenterScreen"
    /// - SizeToContent="WidthAndHeight"
    /// - Background="{StaticResource midGray}"
    /// </summary>
    public class BaseDialog : Window {
        public static readonly DependencyProperty DialogTitleProperty =
            DependencyProperty.Register("DialogTitle", typeof(string), typeof(BaseDialog),
                new PropertyMetadata(string.Empty));

        public DialogButtons Buttons { get; set; } = DialogButtons.OkCancel;

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

            //// Set SizeToContent after InitializeComponent so XAML can override if needed
            //this.Loaded += (s, e) => {
            //    if (double.IsNaN(this.Width) && double.IsNaN(this.Height)) {
            //        this.SizeToContent = SizeToContent.WidthAndHeight;
            //    } else if (double.IsNaN(this.Width)) {
            //        this.SizeToContent = SizeToContent.Width;
            //    } else if (double.IsNaN(this.Height)) {
            //        this.SizeToContent = SizeToContent.Height;
            //    }
            //};
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
            this.DialogResult = false;
            this.Close();
        }
    }
}
