using System.Windows;
using System.Windows.Input;

namespace pictureviewer {
    /// <summary>
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog : Window {
        public AboutDialog() {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(AboutDialog_KeyDown);
        }

        void AboutDialog_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                this.Close();
            }
        }

        private void ok_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }
}
