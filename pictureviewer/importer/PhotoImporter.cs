using Pictureviewer.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using static Pictureviewer.Importer.ImportPhotosDialog;

namespace Pictureviewer.Importer {
    public class PhotoImporter {
        private class ImportProgress {
            public int Current { get; set; }
            public int Total { get; set; }
            public string CurrentFile { get; set; }
        }

        private class ImportState {
            public ImportSource source;
            public string seriesName;
            public IProgress<ImportProgress> progress;
            public Func<bool> isCancelled;
        }

        public static async void ImportPhotos() {
            var dialog = new ImportPhotosDialog(RootControl.SdCardRoot);
            bool? result = dialog.ShowDialog();

            if (result != true) return;

            var progressDialog = new ImportProgressDialog();
            progressDialog.Show();
            var progress = new Progress<ImportProgress>(p => {
                progressDialog.UpdateProgress(p.Current, p.Total, p.CurrentFile);
            });

            var state = new ImportState {
                source = dialog.SelectedSource,
                seriesName = dialog.SeriesName,
                progress = progress,
                isCancelled = () => progressDialog.IsCancelled
            };

            int count = await CopyAndRenameFiles(state);
            progressDialog.Close();

            if (progressDialog.IsCancelled) {
                MessageBox.Show($"Import cancelled. {count} photos were imported before cancellation.", "Import Cancelled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            } else {
                MessageBox.Show($"Successfully imported {count} photos.", "Import Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static async Task<int> CopyAndRenameFiles(ImportState state) {
            Dictionary<string, DateTime> photoDates = await Task.Run(() => GetSourcePhotosWithDates(state.source));
            if (state.source == ImportSource.SDCard) {
                return await ProcessPhotos(state, photoDates, 
                    async (photo, destPath) => await Task.Run(() => File.Copy(photo, destPath, false)));
            } else {
                string zip = FindLatestZipFile();
                if (zip == null) return 0;
                using (ZipArchive archive = ZipFile.OpenRead(zip)) {
                    return await ProcessPhotos(state, photoDates,
                        async (photo, destPath) => {
                            string entryName = Path.GetFileName(photo);
                            ZipArchiveEntry entry = archive.Entries.FirstOrDefault(e => e.Name == entryName);
                            if (entry != null) {
                                await Task.Run(() => entry.ExtractToFile(destPath, false));
                            }
                        });
                }
            }
        }

        private static async Task<int> ProcessPhotos(ImportState state,
            Dictionary<string, DateTime> photoDates, Func<string, string, Task> copyFileAsync) {

            // Group by date ("Key") and sort files
            var groupedByDate = photoDates
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .GroupBy(kvp => kvp.Value.Date)
                .OrderBy(g => g.Key)
                .ToList();

            // Prepare file ID counters for each date directory
            var fileIdCounters = new Dictionary<DateTime, int>();
            foreach (var dateGroup in groupedByDate) {
                string destDir = DestDirectory(dateGroup.Key, state.seriesName);
                Directory.CreateDirectory(destDir);
                int existingFiles = Directory.GetFiles(destDir).Length;
                fileIdCounters[dateGroup.Key] = existingFiles + 1;
            }

            // Copy files
            int totalImported = 0;
            foreach (var dateGroup in groupedByDate) {
                string destDir = DestDirectory(dateGroup.Key, state.seriesName);
                foreach (var photo in dateGroup) {
                    if (state.isCancelled()) {
                        return totalImported;
                    }

                    state.progress?.Report(new ImportProgress {
                        Current = totalImported + 1,
                        Total = photoDates.Count,
                        CurrentFile = photo.Key
                    });

                    string extension = Path.GetExtension(photo.Key).ToLower();
                    string destFileName = $"{dateGroup.Key.ToString("yyyy-MM-dd")} {state.seriesName} {fileIdCounters[dateGroup.Key]:D4}{extension}";
                    string destPath = Path.Combine(destDir, destFileName);

                    await copyFileAsync(photo.Key, destPath);

                    fileIdCounters[dateGroup.Key]++;
                    totalImported++;
                }
            }

            return totalImported;
        }

        private static string DestDirectory(DateTime date , string seriesName) {
            string dateStr = date.ToString("yyyy-MM-dd");
            return Path.Combine(RootControl.ImportDestinationRoot, $"{dateStr} {seriesName}");
        }

        private static Dictionary<string, DateTime> GetSourcePhotosWithDates(ImportSource source) {
            var photoDates = new Dictionary<string, DateTime>();
            string[] imageExtensions = { ".jpg", ".jpeg", ".raw", ".heic" };

            try {
                if (source == ImportPhotosDialog.ImportSource.SDCard) {
                    Debug.Assert(Directory.Exists(RootControl.SdCardRoot));
                    // Scan all subdirectories of SD card root
                    photoDates = Directory.GetDirectories(RootControl.SdCardRoot)
                        .SelectMany(dir => imageExtensions.SelectMany(ext => Directory.GetFiles(dir, "*" + ext, SearchOption.AllDirectories)))
                        .ToDictionary(file => file, file => GetPhotoDateFromFile(file));
                } else { // iCloud
                    if (!Directory.Exists(RootControl.DownloadsRoot)) {
                        return photoDates;
                    }

                    string zip = FindLatestZipFile();
                    if (zip != null) {
                        // Read EXIF from zip entries in memory
                        using (ZipArchive archive = ZipFile.OpenRead(zip)) {
                            foreach (var entry in archive.Entries) {
                                string ext = Path.GetExtension(entry.FullName).ToLower();
                                if (imageExtensions.Contains(ext)) {
                                    using (var memoryStream = new MemoryStream()) {
                                        entry.Open().CopyTo(memoryStream);
                                        photoDates[entry.Name] = GetPhotoDateFromStream(memoryStream) ?? DateTime.Now;
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Error getting source files: {ex.Message}");
            }

            return photoDates;
        }

        // Find the most recent "iCloud Photos*.zip" file, or null if none found
        private static string FindLatestZipFile() {
            return Directory.GetFiles(RootControl.DownloadsRoot, "iCloud Photos*.zip")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .FirstOrDefault();
        }

        // Get EXIF date from stream, returns null if not found or on error
        private static DateTime? GetPhotoDateFromStream(Stream stream) {
            try {
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.None);
                if (decoder.Frames.Count > 0 && decoder.Frames[0].Metadata is BitmapMetadata metadata) {
                    // Path: /app1/ifd/exif/{ushort=36867} is DateTimeOriginal
                    // Path: /app1/ifd/exif/{ushort=36868} is DateTimeDigitized
                    object dateObj = metadata.GetQuery("/app1/ifd/exif/{ushort=36867}")
                        ?? metadata.GetQuery("/app1/ifd/exif/{ushort=36868}");

                    if (dateObj is string dateStr && DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)) {
                        return date;
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Error reading EXIF from stream: {ex.Message}");
            }
            return null;
        }

        // Get photo date from file: try EXIF first, fall back to file creation time
        private static DateTime GetPhotoDateFromFile(string filePath) {
            using (var stream = File.OpenRead(filePath)) {
                return GetPhotoDateFromStream(stream) ?? File.GetCreationTime(filePath);
            }
        }
    }
}
