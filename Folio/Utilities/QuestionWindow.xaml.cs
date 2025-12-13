#nullable disable
namespace Folio.Utilities {
    public partial class QuestionWindow : BaseDialog {
        public QuestionWindow() {
            this.InitializeComponent();
            box.Focus();
        }

        public string Result {
            get { return box.Text; }
            set { box.Text = value; box.SelectAll(); }
        }
    }
}