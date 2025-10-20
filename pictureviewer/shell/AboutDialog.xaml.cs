using Pictureviewer.Utilities;
using System.Windows;

namespace Pictureviewer.Shell {
    /// <summary>
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog : BaseDialog {
        public AboutDialog() {
            InitializeComponent();
            DialogTitle = "Folio";
            Buttons = DialogButtons.Ok;
        }

        private void okButton_Click(object sender, RoutedEventArgs e) {
            OnOk();
        }
    }
}
