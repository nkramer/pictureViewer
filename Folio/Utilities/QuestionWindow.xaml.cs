using System.Windows;
using System.Windows.Input;

namespace Folio.Utilities {
    /// <summary>
    /// Interaction logic for QuestionWindow.xaml
    /// </summary>
    public partial class QuestionWindow : Window {
        public QuestionWindow() {
            this.InitializeComponent();
            box.Focus();
            this.KeyDown += new KeyEventHandler(QuestionWindow_KeyDown);
            // Insert code required on object creation below this point.
        }

        void QuestionWindow_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                cancel_Click(null, null);
            }
        }

        public string Result {
            get { return box.Text; }
            set { box.Text = value; box.SelectAll(); }
        }

        public string Label {
            get { return textBlock.Text; }
            set { textBlock.Text = value; }
        }

        private void ok_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }

        private void cancel_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }
    }
}