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
using Pictureviewer.Shell;
using static Pictureviewer.Importer.ImportPhotosDialog;

namespace Pictureviewer.Importer {
    public class PhotoImporter {
        public class ImportProgress {
            public int Current { get; set; }
            public int Total { get; set; }
            public string CurrentFile { get; set; }
        }

        private class PhotoFile {
            public string SourcePath { get; set; }
            public DateTime DateTaken { get; set; }
            public string Extension { get; set; }
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

            int count = await CopyAndRenameFiles(
                dialog.SelectedSource, dialog.SeriesName,
                progress, () => progressDialog.IsCancelled);
            progressDialog.Close();

            if (progressDialog.IsCancelled) {
                MessageBox.Show($"Import cancelled. {count} photos were imported before cancellation.", "Import Cancelled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            } else {
                MessageBox.Show($"Successfully imported {count} photos.", "Import Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static async Task<int> CopyAndRenameFiles(
            ImportSource source,
            string seriesName,
            IProgress<ImportProgress> progress,
            Func<bool> isCancelled) {

            // Get all photos with their dates
            Dictionary<string, DateTime> photoDates = await Task.Run(() => GetSourcePhotosWithDates(source));

            // Build photo file list sorted by source path
            List<PhotoFile> photoFiles = photoDates
                .Select(kvp => new PhotoFile {
                    SourcePath = kvp.Key,
                    DateTaken = kvp.Value,
                    Extension = Path.GetExtension(kvp.Key).ToLower()
                })
                .OrderBy(p => p.SourcePath, StringComparer.Ordinal)
                .ToList();

            var groupedByDate = photoFiles.GroupBy(p => p.DateTaken.Date)
                .OrderBy(g => g.Key).ToList();

            // Prepare file ID counters for each date directory
            var fileIdCounters = new Dictionary<string, int>();
            foreach (var dateGroup in groupedByDate) {
                string dateStr = dateGroup.Key.ToString("yyyy-MM-dd");
                string destDir = Path.Combine(RootControl.ImportDestinationRoot, $"{dateStr} {seriesName}");
                Directory.CreateDirectory(destDir);

                int existingFiles = Directory.GetFiles(destDir).Length;
                fileIdCounters[dateStr] = existingFiles + 1;
            }

            // Import files
            int totalImported = 0;
            if (source == ImportSource.SDCard) {
                // For SD Card: copy files directly
                foreach (var dateGroup in groupedByDate) {
                    string dateStr = dateGroup.Key.ToString("yyyy-MM-dd");
                    string destDir = Path.Combine(RootControl.ImportDestinationRoot, $"{dateStr} {seriesName}");

                    foreach (var photo in dateGroup.OrderBy(p => p.SourcePath)) {
                        if (isCancelled()) {
                            return totalImported;
                        }

                        progress?.Report(new ImportProgress { Current = totalImported + 1, Total = photoFiles.Count, CurrentFile = photo.SourcePath });

                        string destFileName = GetDestinationFileName(dateStr, seriesName, fileIdCounters[dateStr], photo.Extension);
                        string destPath = Path.Combine(destDir, destFileName);

                        await Task.Run(() => File.Copy(photo.SourcePath, destPath, false));

                        fileIdCounters[dateStr]++;
                        totalImported++;
                    }
                }
            } else {
                // For iCloud: open zip once and extract files
                var zipFiles = Directory.GetFiles(RootControl.DownloadsRoot, "iCloud Photos*.zip")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.LastWriteTime)
                    .ToList();

                if (zipFiles.Count > 0) {
                    var mostRecentZip = zipFiles[0].FullName;

                    using (ZipArchive archive = ZipFile.OpenRead(mostRecentZip)) {
                        // Create lookup from entry name to photo file
                        var photoLookup = photoFiles.ToDictionary(p => Path.GetFileName(p.SourcePath), p => p);

                        foreach (var dateGroup in groupedByDate) {
                            string dateStr = dateGroup.Key.ToString("yyyy-MM-dd");
                            string destDir = Path.Combine(RootControl.ImportDestinationRoot, $"{dateStr} {seriesName}");

                            foreach (var photo in dateGroup.OrderBy(p => p.SourcePath)) {
                                if (isCancelled()) {
                                    return totalImported;
                                }

                                progress?.Report(new ImportProgress { Current = totalImported + 1, Total = photoFiles.Count, CurrentFile = photo.SourcePath });

                                // Find the zip entry by filename
                                string entryName = Path.GetFileName(photo.SourcePath);
                                var entry = archive.Entries.FirstOrDefault(e => e.Name == entryName);

                                if (entry != null) {
                                    string destFileName = GetDestinationFileName(dateStr, seriesName, fileIdCounters[dateStr], photo.Extension);
                                    string destPath = Path.Combine(destDir, destFileName);

                                    await Task.Run(() => entry.ExtractToFile(destPath, false));

                                    fileIdCounters[dateStr]++;
                                    totalImported++;
                                }
                            }
                        }
                    }
                }
            }

            return totalImported;
        }

        private static string GetDestinationFileName(string dateStr, string seriesName, int fileId, string extension) {
            return $"{dateStr} {seriesName} {fileId:D4}{extension}";
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

                    // Find the most recent "iCloud Photos*.zip" file
                    var zipFiles = Directory.GetFiles(RootControl.DownloadsRoot, "iCloud Photos*.zip")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(fi => fi.LastWriteTime)
                        .ToList();

                    if (zipFiles.Count > 0) {
                        var mostRecentZip = zipFiles[0].FullName;

                        // Read EXIF from zip entries in memory
                        using (ZipArchive archive = ZipFile.OpenRead(mostRecentZip)) {
                            foreach (var entry in archive.Entries) {
                                string ext = Path.GetExtension(entry.FullName).ToLower();
                                if (imageExtensions.Contains(ext)) {
                                    // Extract to memory stream and read EXIF
                                    using (var memoryStream = new MemoryStream()) {
                                        entry.Open().CopyTo(memoryStream);
                                        memoryStream.Position = 0;

                                        DateTime dateTaken = GetPhotoDateFromStream(memoryStream) ?? DateTime.Now;

                                        // Use entry name as the key (will be matched later in ImportPhotosAsync)
                                        photoDates[entry.Name] = dateTaken;
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

        // Extract EXIF date from BitmapDecoder, returns null if not found
        private static DateTime? ExtractExifDate(BitmapDecoder decoder) {
            if (decoder.Frames.Count > 0) {
                BitmapMetadata metadata = decoder.Frames[0].Metadata as BitmapMetadata;
                if (metadata != null) {
                    // Path: /app1/ifd/exif/{ushort=36867} is DateTimeOriginal
                    // Path: /app1/ifd/exif/{ushort=36868} is DateTimeDigitized
                    object dateObj = metadata.GetQuery("/app1/ifd/exif/{ushort=36867}")
                        ?? metadata.GetQuery("/app1/ifd/exif/{ushort=36868}");
                    if (dateObj is string dateStr) {
                        if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)) {
                            return date;
                        }
                    }
                }
            }
            return null;
        }

        // Get EXIF date from stream
        private static DateTime? GetPhotoDateFromStream(Stream stream) {
            try {
                stream.Position = 0;
                BitmapDecoder decoder = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.None, BitmapCacheOption.None);
                return ExtractExifDate(decoder) ?? DateTime.Now;
            } catch (Exception ex) {
                Debug.WriteLine($"Error reading EXIF from stream: {ex.Message}");
                return DateTime.Now;
            }
        }

        // Get EXIF date, if that doesn't work use file timestamp
        private static DateTime GetPhotoDateFromFile(string filePath) {
            using (var stream = File.OpenRead(filePath)) {
                return GetPhotoDateFromStream(stream) ?? File.GetCreationTime(filePath);
            }
        }
    }
}
