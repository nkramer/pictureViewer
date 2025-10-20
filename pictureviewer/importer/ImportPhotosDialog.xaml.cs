using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Pictureviewer.Utilities;

namespace Pictureviewer.Importer {
    public partial class ImportPhotosDialog : BaseDialog {
        public enum ImportSource {
            SDCard,
            iCloud
        }

        public ImportSource SelectedSource { get; private set; }
        public string SeriesName { get; private set; }
        private string sdCardRoot;

        public ImportPhotosDialog(string sdCardRoot) {
            DialogTitle = "Copy photos from external source";
            this.sdCardRoot = sdCardRoot;
            InitializeComponent();

            this.Loaded += new RoutedEventHandler(ImportPhotosDialog_Loaded);
        }

        private void ImportPhotosDialog_Loaded(object sender, RoutedEventArgs e) {
            // Check if SD card root exists
            bool sdCardExists = Directory.Exists(sdCardRoot);
            sdCardRadio.IsEnabled = sdCardExists;

            // If SD card doesn't exist, select iCloud by default
            if (!sdCardExists) {
                iCloudRadio.IsChecked = true;
            }

            seriesNameTextBox.Focus();
        }

        protected override void OnOk() {
            // Validate series name
            SeriesName = seriesNameTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(SeriesName)) {
                MessageBox.Show("Please enter a series name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Set selected source
            if (sdCardRadio.IsChecked == true) {
                SelectedSource = ImportSource.SDCard;
            } else {
                SelectedSource = ImportSource.iCloud;
            }

            base.OnOk();
        }
    }
}
