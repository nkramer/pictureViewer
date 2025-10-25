using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Folio.Importer {
    public partial class ImportPhotosDialog : Window {
        public enum ImportSource {
            SDCard,
            iCloud
        }

        public ImportSource SelectedSource { get; private set; }
        public string SeriesName { get; private set; }
        private string sdCardRoot;

        public ImportPhotosDialog(string sdCardRoot) {
            this.sdCardRoot = sdCardRoot;
            InitializeComponent();

            this.KeyDown += new KeyEventHandler(ImportPhotosDialog_KeyDown);
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

        void ImportPhotosDialog_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void ok_Click(object sender, RoutedEventArgs e) {
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

            this.DialogResult = true;
            this.Close();
        }

        private void cancel_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }
    }
}
