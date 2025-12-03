using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace Folio.Tests.TestHelpers {
    /// <summary>
    /// Factory for creating test images with specific EXIF metadata.
    /// </summary>
    public static class TestImageFactory {
        /// <summary>
        /// Creates a JPEG image with EXIF DateTimeOriginal metadata.
        /// </summary>
        public static void CreateJpegWithExifDate(string path, DateTime date) {
            // Create a simple 100x100 bitmap
            using (var bitmap = new Bitmap(100, 100)) {
                using (var graphics = Graphics.FromImage(bitmap)) {
                    graphics.Clear(Color.Blue);
                }

                // Set EXIF DateTimeOriginal (tag 0x9003 / 36867)
                // Format: "yyyy:MM:dd HH:mm:ss"
                string exifDateString = date.ToString("yyyy:MM:dd HH:mm:ss");

                // Get PropertyItem from the bitmap
                PropertyItem propItem = (PropertyItem)Activator.CreateInstance(typeof(PropertyItem), true);
                propItem.Id = 0x9003; // DateTimeOriginal
                propItem.Type = 2; // ASCII
                propItem.Value = Encoding.ASCII.GetBytes(exifDateString + '\0');
                propItem.Len = propItem.Value.Length;

                bitmap.SetPropertyItem(propItem);

                // Save as JPEG
                bitmap.Save(path, ImageFormat.Jpeg);
            }
        }

        /// <summary>
        /// Creates a minimal JPEG image without EXIF data.
        /// </summary>
        public static void CreateJpegWithoutExif(string path) {
            using (var bitmap = new Bitmap(100, 100)) {
                using (var graphics = Graphics.FromImage(bitmap)) {
                    graphics.Clear(Color.Red);
                }
                bitmap.Save(path, ImageFormat.Jpeg);
            }
        }

        /// <summary>
        /// Creates a test image with a specific file creation date.
        /// </summary>
        public static void CreateJpegWithFileDate(string path, DateTime date) {
            CreateJpegWithoutExif(path);
            File.SetCreationTime(path, date);
            File.SetLastWriteTime(path, date);
        }
    }
}
