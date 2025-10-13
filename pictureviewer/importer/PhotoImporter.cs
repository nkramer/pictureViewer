using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

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

        public static async Task<int> ImportPhotosAsync(
            ImportPhotosDialog.ImportSource source,
            string seriesName,
            string destinationRoot,
            string sdCardRoot,
            string downloadsRoot,
            IProgress<ImportProgress> progress,
            Func<bool> isCancelled) {

            List<string> sourceFiles = GetSourceFiles(source, sdCardRoot, downloadsRoot);
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

                DateTime dateTaken = await Task.Run(() => GetExifDate(file));
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
                string destDir = Path.Combine(destinationRoot, dirName);

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

        private static List<string> GetSourceFiles(ImportPhotosDialog.ImportSource source, string sdCardRoot, string downloadsRoot) {
            var files = new List<string>();
            string[] imageExtensions = { "*.jpg", "*.jpeg", "*.raw", "*.heic" };

            try {
                if (source == ImportPhotosDialog.ImportSource.SDCard) {
                    if (!Directory.Exists(sdCardRoot)) {
                        return files;
                    }

                    // Scan all subdirectories of SD card root
                    foreach (var dir in Directory.GetDirectories(sdCardRoot)) {
                        foreach (var ext in imageExtensions) {
                            files.AddRange(Directory.GetFiles(dir, ext, SearchOption.AllDirectories));
                        }
                    }
                } else { // iCloud
                    if (!Directory.Exists(downloadsRoot)) {
                        return files;
                    }

                    // Find the most recent "iCloud Photos*.zip" file
                    var zipFiles = Directory.GetFiles(downloadsRoot, "iCloud Photos*.zip")
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

        private static DateTime GetExifDate(string filePath) {
            try {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    BitmapDecoder decoder = BitmapDecoder.Create(stream,
                        BitmapCreateOptions.DelayCreation,
                        BitmapCacheOption.None);

                    if (decoder.Frames.Count > 0) {
                        BitmapMetadata metadata = decoder.Frames[0].Metadata as BitmapMetadata;
                        if (metadata != null) {
                            // Try to get DateTaken from EXIF
                            // Path: /app1/ifd/exif/{ushort=36867} is DateTimeOriginal
                            // Path: /app1/ifd/exif/{ushort=36868} is DateTimeDigitized
                            object dateObj = metadata.GetQuery("/app1/ifd/exif/{ushort=36867}");
                            if (dateObj == null) {
                                dateObj = metadata.GetQuery("/app1/ifd/exif/{ushort=36868}");
                            }

                            if (dateObj != null && dateObj is string dateStr) {
                                // EXIF date format: "YYYY:MM:DD HH:MM:SS"
                                if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss",
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None, out DateTime date)) {
                                    return date;
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Error reading EXIF from {filePath}: {ex.Message}");
            }

            // Fallback to file creation time
            return File.GetCreationTime(filePath);
        }
    }
}
