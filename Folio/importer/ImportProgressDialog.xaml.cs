using Folio.Utilities;
using System.IO;
using System.Windows.Controls;

namespace Folio.Importer;
public partial class ImportProgressDialog : BaseDialog {
    public bool IsCancelled { get; private set; }

    public ImportProgressDialog() {
        DialogTitle = "Importing Photos";
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
