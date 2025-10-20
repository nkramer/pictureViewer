using System;
using System.IO;
using System.Windows;
using Pictureviewer.Utilities;
using System.Windows.Controls;

namespace Pictureviewer.Importer {
    public partial class ImportProgressDialog : BaseDialog {
        public bool IsCancelled { get; private set; }
        private Button templateCancelButton;

        public ImportProgressDialog() {
            DialogTitle = "Importing Photos";
            InitializeComponent();
            IsCancelled = false;
        }

        // TODO - Can we not just call find name within OnCancel? 
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            // Get reference to the Cancel button from the template
            if (this.Template.FindName("CancelButton", this) is System.Windows.Controls.Button cancelBtn) {
                templateCancelButton = cancelBtn;
            }
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
            if (templateCancelButton != null) {
                templateCancelButton.IsEnabled = false;
                templateCancelButton.Content = "Cancelling...";
            }
            // Note: Don't call base.OnCancel() because we don't want to close the dialog
        }
    }
}
