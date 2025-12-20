#nullable disable
using System.IO;
using System.Windows.Controls;

namespace Folio.Utilities;

public partial class ProgressDialog : BaseDialog {
    public bool IsCancelled { get; private set; }

    public ProgressDialog(string title = "Progress") {
        DialogTitle = title;
        InitializeComponent();
        IsCancelled = false;
    }

    public void UpdateProgress(int current, int total, string currentFile) {
        statusText.Text = $"{current}/{total}";
        progressBar.Maximum = total;
        progressBar.Value = current;

        if (!string.IsNullOrEmpty(currentFile)) {
            filenameText.Text = Path.GetFileName(currentFile);
        }
    }

    protected override void OnCancel() {
        IsCancelled = true;
        if (this.Template.FindName("CancelButton", this) is Button cancelButton) {
            cancelButton.IsEnabled = false;
            cancelButton.Content = "Cancelling...";
        }
        // Note: Don't call base.OnCancel() because we don't want to close the dialog
    }
}
