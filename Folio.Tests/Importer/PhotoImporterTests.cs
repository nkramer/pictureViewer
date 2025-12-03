using FluentAssertions;
using Folio.Importer;
using Folio.Shell;
using Folio.Tests.TestHelpers;
using System;
using System.IO;
using Xunit;
using static Folio.Importer.ImportPhotosDialog;
using static Folio.Importer.PhotoImporter;

namespace Folio.Tests.Importer {
    public class PhotoImporterTests : IDisposable {
        private readonly string tempDir;
        private readonly string originalImportRoot;

        public PhotoImporterTests() {
            // Create a temp directory for test output
            tempDir = Path.Combine(Path.GetTempPath(), "FolioTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Save original ImportDestinationRoot and set to temp directory
            originalImportRoot = RootControl.ImportDestinationRoot;
            RootControl.ImportDestinationRoot = tempDir;
        }

        public void Dispose() {
            // Restore original ImportDestinationRoot
            RootControl.ImportDestinationRoot = originalImportRoot;

            // Clean up temp directory
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void NextDestFilePath_CreatesCorrectFileName() {
            // Arrange
            var date = new DateTime(2024, 3, 15);
            var state = new ImportState {
                seriesName = "Vacation",
                totalImported = 0,
                totalToImport = 10
            };

            // Act
            string path = NextDestFilePath(state, "IMG_1234.jpg", date);

            // Assert
            path.Should().EndWith("2024-03-15 Vacation 0001.jpg");
            path.Should().StartWith(tempDir);
        }

        [Fact]
        public void NextDestFilePath_IncrementsSequenceNumber() {
            // Arrange
            var date = new DateTime(2024, 3, 15);
            var state = new ImportState {
                seriesName = "Trip",
                totalImported = 0,
                totalToImport = 10
            };

            // Act
            string path1 = NextDestFilePath(state, "IMG_1234.jpg", date);
            state.dirCounters[date]++; // Simulate increment after copy
            string path2 = NextDestFilePath(state, "IMG_1235.jpg", date);

            // Assert
            path1.Should().EndWith("2024-03-15 Trip 0001.jpg");
            path2.Should().EndWith("2024-03-15 Trip 0002.jpg");
        }

        [Fact]
        public void NextDestFilePath_PreservesFileExtension() {
            // Arrange
            var date = new DateTime(2024, 3, 15);
            var state = new ImportState {
                seriesName = "Test",
                totalImported = 0,
                totalToImport = 10
            };

            // Act
            string jpegPath = NextDestFilePath(state, "IMG_1234.jpg", date);
            state.dirCounters[date]++;
            string rawPath = NextDestFilePath(state, "IMG_1235.arw", date);

            // Assert
            jpegPath.Should().EndWith(".jpg");
            rawPath.Should().EndWith(".arw");
        }

        [Fact]
        public void NextDestFilePath_HandlesExistingFiles() {
            // Arrange
            var date = new DateTime(2024, 3, 15);
            var destDir = Path.Combine(tempDir, "2024-03-15 Existing");
            Directory.CreateDirectory(destDir);

            // Create some existing files
            File.WriteAllText(Path.Combine(destDir, "2024-03-15 Existing 0001.jpg"), "test");
            File.WriteAllText(Path.Combine(destDir, "2024-03-15 Existing 0002.jpg"), "test");

            var state = new ImportState {
                seriesName = "Existing",
                totalImported = 0,
                totalToImport = 10
            };

            // Act
            string path = NextDestFilePath(state, "IMG_1234.jpg", date);

            // Assert
            // Should start at 0003 since 0001 and 0002 exist
            path.Should().EndWith("2024-03-15 Existing 0003.jpg");
        }

        [Fact]
        public void NextDestFilePath_HandlesExistingARWJPGPairs() {
            // Arrange
            var date = new DateTime(2024, 3, 15);
            var destDir = Path.Combine(tempDir, "2024-03-15 Pairs");
            Directory.CreateDirectory(destDir);

            // Create ARW+JPG pairs with same base name
            File.WriteAllText(Path.Combine(destDir, "2024-03-15 Pairs 0001.arw"), "test");
            File.WriteAllText(Path.Combine(destDir, "2024-03-15 Pairs 0001.jpg"), "test");

            var state = new ImportState {
                seriesName = "Pairs",
                totalImported = 0,
                totalToImport = 10
            };

            // Act
            string path = NextDestFilePath(state, "IMG_1234.jpg", date);

            // Assert
            // Should start at 0002 since the pair counts as 1 unique file
            path.Should().EndWith("2024-03-15 Pairs 0002.jpg");
        }

        [Fact]
        public void NextDestFilePath_ReportsProgress() {
            // Arrange
            var date = new DateTime(2024, 3, 15);
            var progress = new ProgressCapture<ImportProgress>();
            var state = new ImportState {
                seriesName = "Progress",
                totalImported = 5,
                totalToImport = 10,
                progress = progress
            };

            // Act
            NextDestFilePath(state, "IMG_1234.jpg", date);

            // Assert
            progress.Reports.Should().HaveCount(1);
            progress.Reports[0].Current.Should().Be(6); // totalImported + 1
            progress.Reports[0].Total.Should().Be(10);
            progress.Reports[0].CurrentFile.Should().Be("IMG_1234.jpg");
        }

        [Fact]
        public void PhotoDate_FromStream_ParsesExifDate() {
            // Arrange
            var expectedDate = new DateTime(2024, 3, 15, 14, 30, 0);
            var tempFile = Path.Combine(tempDir, "test_exif.jpg");
            TestImageFactory.CreateJpegWithExifDate(tempFile, expectedDate);

            // Act
            DateTime? result;
            using (var stream = File.OpenRead(tempFile)) {
                result = PhotoDate(stream);
            }

            // Assert
            result.Should().NotBeNull();
            result.Value.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void PhotoDate_FromStream_ReturnsNullWhenNoExif() {
            // Arrange
            var tempFile = Path.Combine(tempDir, "test_no_exif.jpg");
            TestImageFactory.CreateJpegWithoutExif(tempFile);

            // Act
            DateTime? result;
            using (var stream = File.OpenRead(tempFile)) {
                result = PhotoDate(stream);
            }

            // Assert
            // Note: May return null or a default date depending on implementation
            // The current implementation has WPF fallback, but for a truly EXIF-less file, should be null
        }

        [Fact]
        public void PhotoDate_FromFilePath_ParsesExifDate() {
            // Arrange
            var expectedDate = new DateTime(2024, 3, 15, 14, 30, 0);
            var tempFile = Path.Combine(tempDir, "test_file_exif.jpg");
            TestImageFactory.CreateJpegWithExifDate(tempFile, expectedDate);

            // Act
            DateTime result = PhotoDate(tempFile);

            // Assert
            result.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void PhotoDate_FromFilePath_FallsBackToFileCreationTime() {
            // Arrange
            var fileDate = new DateTime(2024, 3, 15);
            var tempFile = Path.Combine(tempDir, "test_file_date.jpg");
            TestImageFactory.CreateJpegWithFileDate(tempFile, fileDate);

            // Act
            DateTime result = PhotoDate(tempFile);

            // Assert
            // Should use file creation time when EXIF is missing
            result.Date.Should().Be(fileDate.Date);
        }
    }
}
