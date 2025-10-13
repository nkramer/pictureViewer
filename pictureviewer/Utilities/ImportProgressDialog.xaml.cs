using System;
using System.Windows;

namespace Pictureviewer.Utilities {
    public partial class ImportProgressDialog : Window {
        public ImportProgressDialog() {
            InitializeComponent();
        }

        public void UpdateProgress(int current, int total) {
            statusText.Text = $"{current}/{total}";
            progressBar.Maximum = total;
            progressBar.Value = current;
        }
    }
}
