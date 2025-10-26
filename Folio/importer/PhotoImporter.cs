using Folio.Shell;
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
using static Folio.Importer.ImportPhotosDialog;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace Folio.Importer {
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
            public readonly Dictionary<DateTime, int> dirCounters = new Dictionary<DateTime, int>();
            public int totalImported = 0;
            public int totalToImport = -1;
            public void IncrementCounters(DateTime date) {
                dirCounters[date]++;
                totalImported++;
            }
        }

        public static async void ImportPhotos() {
            var dialog = new ImportPhotosDialog(RootControl.SdCardRoot);
            bool? result = dialog.ShowDialog();
            if (result != true) return;

            var progressDialog = new ImportProgressDialog();
            var progress = new Progress<ImportProgress>(p => {
                progressDialog.UpdateProgress(p.Current, p.Total, p.CurrentFile);
            });

            var state = new ImportState {
                source = dialog.SelectedSource,
                seriesName = dialog.SeriesName,
                progress = progress,
                isCancelled = () => progressDialog.IsCancelled
            };

            Task task = state.source == ImportSource.SDCard
                ? CopySDCardFiles(state)
                : CopyiCloudFiles(state);
        
            progressDialog.Show();
            await task;
            progressDialog.Close();

            if (progressDialog.IsCancelled) {
                MessageBox.Show($"Import cancelled. {state.totalImported} photos were imported before cancellation.", "Import Cancelled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            } else {
                MessageBox.Show($"Successfully imported {state.totalImported} photos.", "Import Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static async Task CopySDCardFiles(ImportState state) {
            string[] imageExtensions = { ".jpg", ".jpeg", ".raw", ".arw", ".heic" };

            // Get all files
            var files = System.IO.Directory.GetDirectories(RootControl.SdCardRoot)
                .SelectMany(dir => imageExtensions.SelectMany(ext =>
                    System.IO.Directory.GetFiles(dir, "*" + ext, SearchOption.AllDirectories)))
                .ToList();

            // Group files by directory and base name, so DSC04410.ARW and DSC04410.JPG are grouped together
            var fileGroups = files
                .GroupBy(f => Path.Combine(Path.GetDirectoryName(f), Path.GetFileNameWithoutExtension(f)))
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            state.totalToImport = files.Count;

            foreach (var group in fileGroups) {
                if (state.isCancelled()) return;

                // Get date from first file in group (they should all have the same date)
                string firstFile = group.OrderBy(f => f, StringComparer.Ordinal).First();
                DateTime date = PhotoDate(firstFile);

                // Copy all files in the group with the same sequence number
                foreach (string filename in group.OrderBy(f => f, StringComparer.Ordinal)) {
                    string destPath = NextDestFilePath(state, filename, date);
                    await Task.Run(() => File.Copy(filename, destPath, false));
                    state.totalImported++;
                }

                // Increment counter only once per group (not per file)
                state.dirCounters[date]++;
            }
        }

        private static async Task CopyiCloudFiles(ImportState state) {
            // get latest zip file
            string zip = System.IO.Directory.GetFiles(RootControl.DownloadsRoot, "iCloud Photos*.zip")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .FirstOrDefault();
            if (zip == null) return;

            using (ZipArchive archive = ZipFile.OpenRead(zip)) {
                string[] imageExtensions = { ".jpg", ".jpeg", ".raw", ".heic" };
                var entries = archive.Entries
                    .Where(e => imageExtensions.Contains(Path.GetExtension(e.FullName).ToLower()))
                    .OrderBy(e => e.Name, StringComparer.Ordinal)
                    .ToList();
                state.totalToImport = entries.Count;

                foreach (var entry in entries) {
                    if (state.isCancelled()) return;
                    DateTime date = PhotoDate(entry);
                    string destPath = NextDestFilePath(state, entry.Name, date);
                    await Task.Run(() => entry.ExtractToFile(destPath, false));
                    state.IncrementCounters(date);
                }
            }
        }

        // calculate the next file name in the destination directory, 
        // make the directory exist, and increment the progress counter
        private static string NextDestFilePath(ImportState state, string srcName, DateTime date) {
            state.progress?.Report(new ImportProgress {
                Current = state.totalImported + 1,
                Total = state.totalToImport,
                CurrentFile = srcName
            });

            // Ensure directory exists and get file ID
            string dateStr = date.ToString("yyyy-MM-dd");
            string destDir = Path.Combine(RootControl.ImportDestinationRoot, $"{dateStr} {state.seriesName}");
            if (!state.dirCounters.ContainsKey(date)) {
                System.IO.Directory.CreateDirectory(destDir);
                // Count unique base names (without extensions) to handle ARW+JPG pairs
                var existingFiles = System.IO.Directory.Exists(destDir)
                    ? System.IO.Directory.GetFiles(destDir)
                    : new string[0];
                int existingUniqueFiles = existingFiles
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Distinct().Count();
                state.dirCounters[date] = existingUniqueFiles + 1;
            }

            // Extract file
            string extension = Path.GetExtension(srcName).ToLower();
            string destFileName = $"{date.ToString("yyyy-MM-dd")} {state.seriesName} {state.dirCounters[date]:D4}{extension}";
            string destPath = Path.Combine(destDir, destFileName);
            return destPath;
        }

        // Get EXIF date from stream, returns null if not found or on error
        private static DateTime? PhotoDate(Stream stream) {
            try {
                // Try MetadataExtractor first (works better for HEIC files)
                var directories = ImageMetadataReader.ReadMetadata(stream);

                // Look for EXIF SubIFD directory which contains DateTimeOriginal
                var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfdDirectory != null) {
                    // Try DateTimeOriginal first, then DateTimeDigitized
                    if (subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dateTimeOriginal)) {
                        return dateTimeOriginal;
                    }
                    if (subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out DateTime dateTimeDigitized)) {
                        return dateTimeDigitized;
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Error reading metadata with MetadataExtractor: {ex.Message}");

                // Fall back to WPF/WIC for non-HEIC files
                try {
                    stream.Position = 0;
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
                } catch (Exception innerEx) {
                    Debug.WriteLine($"Error reading EXIF from stream with WPF: {innerEx.Message}");
                }
            }
            return null;
        }

        private static DateTime PhotoDate(ZipArchiveEntry entry) {
            using (var zipStream = entry.Open())
            using (var memoryStream = new MemoryStream()) {
                zipStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                return (PhotoDate(memoryStream) ?? DateTime.Now).Date;
            }
        }

        private static DateTime PhotoDate(string filename) {
            using (var stream = File.OpenRead(filename)) {
                return PhotoDate(stream) ?? File.GetCreationTime(filename);
            }
        }
    }
}
