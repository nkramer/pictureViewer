using FluentAssertions;
using Folio.Book;
using Folio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Folio.Tests.Book {
    public class BookPrintingTests {
        private readonly ITestOutputHelper _output;

        public BookPrintingTests(ITestOutputHelper output) {
            _output = output;
        }

        [Fact(Skip = "Long-running test - run manually only")]
        public void PrintBook_2011Utah_MatchesBaseline() {
            string bookPath = @"C:\Users\nickk\source\pictureDatabase\book - 2011 Utah.xml";
            string outputDir = @"C:\Users\nickk\source\psedbtool\test-print";
            string baselineDir = @"C:\Users\nickk\source\psedbtool\test-baseline";

            // Skip test if book file doesn't exist
            if (!File.Exists(bookPath)) {
                _output.WriteLine($"Skipping test - book file not found at: {bookPath}");
                return;
            }

            Exception testException = null;
            List<string> generatedFiles = null;

            var thread = new Thread(() => {
                try {
                    // Set up WPF Application (ensures only one instance per AppDomain)
                    WpfTestHelper.EnsureApplicationInitialized();

                    // Set up output directory
                    Directory.CreateDirectory(outputDir);
                    string tempDbDir = Path.Combine(outputDir, "..");
                    Directory.CreateDirectory(Path.Combine(tempDbDir, "output"));

                    // Initialize RootControl.Instance if needed (required for BookModel.Load)
                    // The RootControl constructor will load the photo database from RootControl.dbDir
                    if (RootControl.Instance == null) {
                        // RootControl constructor loads the database from dbDir automatically
                        var rootControl = new RootControl();
                        _output.WriteLine($"Initialized RootControl with {rootControl.CompleteSet.Length} images from database");
                    }

                    // Load the book using the Load method
                    var bookModel = BookModel.Load(bookPath);

                    // Override dbDir for output (after loading book)
                    string originalDbDir = RootControl.dbDir;
                    RootControl.dbDir = tempDbDir;

                    bookModel.Pages.Should().NotBeEmpty("the book should have at least one page");
                    _output.WriteLine($"Loaded book with {bookModel.Pages.Count} pages");

                    generatedFiles = new List<string>();
                    int pagenum = 0;
                    foreach (PhotoPageModel page in bookModel.Pages) {
                        string filename = Path.Combine(tempDbDir, "output", $"page-{pagenum:D2}.jpg");
                        PageDesigner.PrintPage(page, filename, null);
                        generatedFiles.Add(filename);
                        _output.WriteLine($"Generated page {pagenum}: {Path.GetFileName(filename)}");
                        pagenum++;
                    }

                    // Restore original dbDir
                    RootControl.dbDir = originalDbDir;

                    // Copy generated files to output directory for inspection
                    foreach (var file in generatedFiles) {
                        string destFile = Path.Combine(outputDir, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }

                    _output.WriteLine($"\nGenerated {generatedFiles.Count} pages to: {outputDir}");

                    // Compare with baseline if it exists
                    if (Directory.Exists(baselineDir)) {
                        CompareWithBaseline(outputDir, baselineDir, _output);
                    } else {
                        _output.WriteLine($"\nNo baseline found at: {baselineDir}");
                        _output.WriteLine("To create baseline, copy the generated files from:");
                        _output.WriteLine($"  {outputDir}");
                        _output.WriteLine("To:");
                        _output.WriteLine($"  {baselineDir}");
                    }
                } catch (Exception ex) {
                    testException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (testException != null) {
                throw testException;
            }

            generatedFiles.Should().NotBeNull("files should have been generated");
            generatedFiles.Should().NotBeEmpty("at least one page should have been generated");
        }

        private void CompareWithBaseline(string outputDir, string baselineDir, ITestOutputHelper output) {
            var outputFiles = Directory.GetFiles(outputDir, "page-*.jpg").OrderBy(f => f).ToList();
            var baselineFiles = Directory.GetFiles(baselineDir, "page-*.jpg").OrderBy(f => f).ToList();

            outputFiles.Count.Should().Be(baselineFiles.Count,
                $"the number of generated pages should match the baseline ({baselineFiles.Count} baseline files)");

            var differences = new List<string>();
            for (int i = 0; i < outputFiles.Count; i++) {
                var outputFile = outputFiles[i];
                var baselineFile = baselineFiles[i];

                bool filesMatch = FilesAreIdentical(outputFile, baselineFile);

                if (filesMatch) {
                    output.WriteLine($"Page {i} ({Path.GetFileName(outputFile)}): MATCH");
                } else {
                    var outputInfo = new FileInfo(outputFile);
                    var baselineInfo = new FileInfo(baselineFile);
                    differences.Add($"Page {i} ({Path.GetFileName(outputFile)}): Files differ (Output: {outputInfo.Length} bytes, Baseline: {baselineInfo.Length} bytes)");
                    output.WriteLine($"Page {i} ({Path.GetFileName(outputFile)}): DIFFERENT");
                }
            }

            if (differences.Count > 0) {
                var diffReport = $"Found {differences.Count} pages with differences:\n" +
                                string.Join("\n", differences);
                output.WriteLine($"\n{diffReport}");
                differences.Should().BeEmpty("all pages should match the baseline exactly (byte-for-byte)");
            } else {
                output.WriteLine("\nAll pages match the baseline exactly!");
            }
        }

        private bool FilesAreIdentical(string file1, string file2) {
            var bytes1 = File.ReadAllBytes(file1);
            var bytes2 = File.ReadAllBytes(file2);

            if (bytes1.Length != bytes2.Length) {
                return false;
            }

            for (int i = 0; i < bytes1.Length; i++) {
                if (bytes1[i] != bytes2[i]) {
                    return false;
                }
            }

            return true;
        }
    }
}
