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
            if (state.source == ImportSource.SDCard) {
                return await ProcessSDCardPhotos(state);
            } else {
                return await ProcessiCloudPhotos(state);
            }
        }

        private static async Task<int> ProcessSDCardPhotos(ImportState state) {
            string[] imageExtensions = { ".jpg", ".jpeg", ".raw", ".heic" };

            // Get all files sorted
            var files = Directory.GetDirectories(RootControl.SdCardRoot)
                .SelectMany(dir => imageExtensions.SelectMany(ext =>
                    Directory.GetFiles(dir, "*" + ext, SearchOption.AllDirectories)))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            return await ProcessPhotosInSinglePass(state, files,
                (sourcePath, stream) => GetPhotoDateFromStream(stream) ?? File.GetCreationTime(sourcePath),
                async (sourcePath, destPath) => await Task.Run(() => File.Copy(sourcePath, destPath, false)));
        }

        private static async Task<int> ProcessiCloudPhotos(ImportState state) {
            string zip = FindLatestZipFile();
            if (zip == null) return 0;

            using (ZipArchive archive = ZipFile.OpenRead(zip)) {
                string[] imageExtensions = { ".jpg", ".jpeg", ".raw", ".heic" };

                var entries = archive.Entries
                    .Where(e => imageExtensions.Contains(Path.GetExtension(e.FullName).ToLower()))
                    .OrderBy(e => e.Name, StringComparer.Ordinal)
                    .ToList();

                var fileIdCounters = new Dictionary<DateTime, int>();
                int totalImported = 0;

                foreach (var entry in entries) {
                    if (state.isCancelled()) {
                        return totalImported;
                    }

                    state.progress?.Report(new ImportProgress {
                        Current = totalImported + 1,
                        Total = entries.Count,
                        CurrentFile = entry.Name
                    });

                    // Get date from entry stream
                    DateTime date;
                    using (var stream = entry.Open()) {
                        date = (GetPhotoDateFromStream(stream) ?? DateTime.Now).Date;
                    }
                    string destDir = DestDirectory(date, state.seriesName);

                    // Ensure directory exists and get file ID
                    if (!fileIdCounters.ContainsKey(date)) {
                        Directory.CreateDirectory(destDir);
                        int existingFiles = Directory.GetFiles(destDir).Length;
                        fileIdCounters[date] = existingFiles + 1;
                    }

                    // Extract file
                    string extension = Path.GetExtension(entry.Name).ToLower();
                    string destFileName = $"{date.ToString("yyyy-MM-dd")} {state.seriesName} {fileIdCounters[date]:D4}{extension}";
                    string destPath = Path.Combine(destDir, destFileName);

                    await Task.Run(() => entry.ExtractToFile(destPath, false));
                    fileIdCounters[date]++;
                    totalImported++;
                }

                return totalImported;
            }
        }

        private static async Task<int> ProcessPhotosInSinglePass(
            ImportState state,
            List<string> sourceFiles,
            Func<string, Stream, DateTime> getDateFunc,
            Func<string, string, Task> copyFileAsync) {

            var fileIdCounters = new Dictionary<DateTime, int>();
            int totalImported = 0;

            foreach (var sourceFile in sourceFiles) {
                if (state.isCancelled()) {
                    return totalImported;
                }

                state.progress?.Report(new ImportProgress {
                    Current = totalImported + 1,
                    Total = sourceFiles.Count,
                    CurrentFile = sourceFile
                });

                // Get date for this file
                DateTime dateTime;
                using (var stream = File.OpenRead(sourceFile)) {
                    dateTime = getDateFunc(sourceFile, stream);
                }
                DateTime date = dateTime.Date;

                // Ensure directory exists and get file ID
                if (!fileIdCounters.ContainsKey(date)) {
                    string destDir = DestDirectory(date, state.seriesName);
                    Directory.CreateDirectory(destDir);
                    int existingFiles = Directory.GetFiles(destDir).Length;
                    fileIdCounters[date] = existingFiles + 1;
                }

                // Copy file
                string extension = Path.GetExtension(sourceFile).ToLower();
                string destFileName = $"{date.ToString("yyyy-MM-dd")} {state.seriesName} {fileIdCounters[date]:D4}{extension}";
                string destDir2 = DestDirectory(date, state.seriesName);
                string destPath = Path.Combine(destDir2, destFileName);

                await copyFileAsync(sourceFile, destPath);

                fileIdCounters[date]++;
                totalImported++;
            }

            return totalImported;
        }

        private static string DestDirectory(DateTime date , string seriesName) {
            string dateStr = date.ToString("yyyy-MM-dd");
            return Path.Combine(RootControl.ImportDestinationRoot, $"{dateStr} {seriesName}");
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

    }
}
