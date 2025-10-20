using System;
using System.IO;
using System.Windows;
using Pictureviewer.Utilities;

namespace Pictureviewer.Importer {
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

        private void cancelButton_Click(object sender, RoutedEventArgs e) {
            IsCancelled = true;
            cancelButton.IsEnabled = false;
            cancelButton.Content = "Cancelling...";
        }
    }
}
