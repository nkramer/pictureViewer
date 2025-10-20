using System.Windows;

namespace Pictureviewer.Utilities {
    /// <summary>
    /// Interaction logic for QuestionWindow.xaml
    /// </summary>
    public partial class QuestionWindow : BaseDialog {
        public QuestionWindow() {
            this.InitializeComponent();
            this.Loaded += QuestionWindow_Loaded;
        }

        private void QuestionWindow_Loaded(object sender, RoutedEventArgs e) {
            box.Focus();
        }

        public string Result {
            get { return box.Text; }
            set { box.Text = value; box.SelectAll(); }
        }

        public string Label {
            get { return textBlock.Text; }
            set { textBlock.Text = value; }
        }
    }
}