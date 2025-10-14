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

            int count = await ImportPhotosAsync(
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

        private static async Task<int> ImportPhotosAsync(
            ImportSource source,
            string seriesName,
            IProgress<ImportProgress> progress,
            Func<bool> isCancelled) {

            List<string> sourceFiles = GetSourceFiles(source);
            if (sourceFiles.Count == 0) {
                return 0;
            }

            // Sort files alphabetically
            sourceFiles.Sort(StringComparer.Ordinal);

            // Extract EXIF dates and group by date
            var photoFiles = new List<PhotoFile>();
            int current = 0;

            foreach (var file in sourceFiles) {
                if (isCancelled()) {
                    return 0;
                }

                current++;
                progress?.Report(new ImportProgress { Current = current, Total = sourceFiles.Count, CurrentFile = file });

                DateTime dateTaken = await Task.Run(() => GetPhotoDate(file));
                photoFiles.Add(new PhotoFile {
                    SourcePath = file,
                    DateTaken = dateTaken,
                    Extension = Path.GetExtension(file).ToLower()
                });
            }

            // Group by date
            var groupedByDate = photoFiles
                .GroupBy(p => p.DateTaken.Date)
                .OrderBy(g => g.Key)
                .ToList();

            // Import files
            int totalImported = 0;
            current = 0;

            foreach (var dateGroup in groupedByDate) {
                string dateStr = dateGroup.Key.ToString("yyyy-MM-dd");
                string dirName = $"{dateStr} {seriesName}";
                string destDir = Path.Combine(RootControl.ImportDestinationRoot, dirName);

                Directory.CreateDirectory(destDir);

                // Get existing files to determine starting ID
                var existingFiles = Directory.Exists(destDir)
                    ? Directory.GetFiles(destDir).Length
                    : 0;

                int fileId = existingFiles + 1;

                foreach (var photo in dateGroup.OrderBy(p => p.SourcePath)) {
                    if (isCancelled()) {
                        return totalImported;
                    }

                    current++;
                    progress?.Report(new ImportProgress { Current = current, Total = photoFiles.Count, CurrentFile = photo.SourcePath });

                    string destFileName = $"{dateStr} {seriesName} {fileId:D4}{photo.Extension}";
                    string destPath = Path.Combine(destDir, destFileName);

                    await Task.Run(() => File.Copy(photo.SourcePath, destPath, false));

                    fileId++;
                    totalImported++;
                }
            }

            return totalImported;
        }

        private static Dictionary<string, DateTime> GetSourcePhotosWithDates(ImportSource source) {
            var photoDates = new Dictionary<string, DateTime>();
            string[] imageExtensions = { ".jpg", ".jpeg", ".raw", ".heic" };

            try {
                if (source == ImportPhotosDialog.ImportSource.SDCard) {
                    if (!Directory.Exists(RootControl.SdCardRoot)) {
                        return photoDates;
                    }

                    // Scan all subdirectories of SD card root
                    foreach (var dir in Directory.GetDirectories(RootControl.SdCardRoot)) {
                        foreach (var ext in imageExtensions) {
                            var files = Directory.GetFiles(dir, "*" + ext, SearchOption.AllDirectories);
                            foreach (var file in files) {
                                DateTime dateTaken = GetPhotoDate(file);
                                photoDates[file] = dateTaken;
                            }
                        }
                    }
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

                        // Extract and get .jpg, .jpeg, and .heic files
                        using (ZipArchive archive = ZipFile.OpenRead(mostRecentZip)) {
                            foreach (var entry in archive.Entries) {
                                string ext = Path.GetExtension(entry.FullName).ToLower();
                                if (imageExtensions.Contains(ext)) {
                                    // Extract to memory stream and read EXIF
                                    using (var memoryStream = new MemoryStream()) {
                                        entry.Open().CopyTo(memoryStream);
                                        memoryStream.Position = 0;

                                        DateTime dateTaken = GetPhotoDateFromStream(memoryStream);

                                        // Create temp path for later copying
                                        string tempPath = Path.Combine(Path.GetTempPath(), "PhotoImport_" + Guid.NewGuid().ToString());
                                        Directory.CreateDirectory(tempPath);
                                        string destPath = Path.Combine(tempPath, entry.Name);

                                        // Write stream to temp file
                                        memoryStream.Position = 0;
                                        using (var fileStream = File.Create(destPath)) {
                                            memoryStream.CopyTo(fileStream);
                                        }

                                        photoDates[destPath] = dateTaken;
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

        private static List<string> GetSourceFiles(ImportSource source) {
            var files = new List<string>();
            string[] imageExtensions = { "*.jpg", "*.jpeg", "*.raw", "*.heic" };

            try {
                if (source == ImportPhotosDialog.ImportSource.SDCard) {
                    if (!Directory.Exists(RootControl.SdCardRoot)) {
                        return files;
                    }

                    // Scan all subdirectories of SD card root
                    foreach (var dir in Directory.GetDirectories(RootControl.SdCardRoot)) {
                        foreach (var ext in imageExtensions) {
                            files.AddRange(Directory.GetFiles(dir, ext, SearchOption.AllDirectories));
                        }
                    }
                } else { // iCloud
                    if (!Directory.Exists(RootControl.DownloadsRoot)) {
                        return files;
                    }

                    // Find the most recent "iCloud Photos*.zip" file
                    var zipFiles = Directory.GetFiles(RootControl.DownloadsRoot, "iCloud Photos*.zip")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(fi => fi.LastWriteTime)
                        .ToList();

                    if (zipFiles.Count > 0) {
                        var mostRecentZip = zipFiles[0].FullName;

                        // Extract and get .jpg, .jpeg, and .heic files
                        using (ZipArchive archive = ZipFile.OpenRead(mostRecentZip)) {
                            foreach (var entry in archive.Entries) {
                                string ext = Path.GetExtension(entry.FullName).ToLower();
                                if (ext == ".jpg" || ext == ".jpeg" || ext == ".heic") {
                                    // Create temp path
                                    string tempPath = Path.Combine(Path.GetTempPath(), "PhotoImport_" + Guid.NewGuid().ToString());
                                    Directory.CreateDirectory(tempPath);
                                    string destPath = Path.Combine(tempPath, entry.Name);
                                    entry.ExtractToFile(destPath);
                                    files.Add(destPath);
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Error getting source files: {ex.Message}");
            }

            return files;
        }

        // Try EXIF from stream, if that doesn't work use current time
        private static DateTime GetPhotoDateFromStream(Stream stream) {
            try {
                stream.Position = 0;
                BitmapDecoder decoder = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.None, BitmapCacheOption.None);

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
            } catch (Exception ex) {
                Debug.WriteLine($"Error reading EXIF from stream: {ex.Message}");
            }

            return DateTime.Now;
        }

        // Try EXIF, if that doesn't work use file timestamp
        private static DateTime GetPhotoDate(string filePath) {
            try {
                BitmapDecoder decoder = BitmapDecoder.Create(new Uri(filePath),
                    BitmapCreateOptions.None, BitmapCacheOption.None);

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
            } catch (Exception ex) {
                Debug.WriteLine($"Error reading EXIF from {filePath}: {ex.Message}");
            }

            return File.GetCreationTime(filePath);
        }
    }
}
