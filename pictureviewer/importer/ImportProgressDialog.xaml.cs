using System;
using System.IO;
using System.Windows;

namespace Folio.Importer {
    public partial class ImportProgressDialog : Window {
        public bool IsCancelled { get; private set; }

        public ImportProgressDialog() {
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

        private void cancel_Click(object sender, RoutedEventArgs e) {
            IsCancelled = true;
            cancelButton.IsEnabled = false;
            cancelButton.Content = "Cancelling...";
        }
    }
}
