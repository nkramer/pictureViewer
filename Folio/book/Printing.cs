using Folio.Core;
using Folio.Shell;
using Folio.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Folio.Book;

public static class Printing {
    public static async void ExportBookToHtml(BookModel book) {
        string outputDir = RootControl.dbDir + @"\html-output";
        string imagesDir = Path.Combine(outputDir, "images");

        // Create output directories if they don't exist
        if (!Directory.Exists(outputDir)) {
            Directory.CreateDirectory(outputDir);
        }
        if (!Directory.Exists(imagesDir)) {
            Directory.CreateDirectory(imagesDir);
        }

        // Extract embedded CSS and JS files to output directory
        ExtractEmbeddedResource("html.styles.css", Path.Combine(outputDir, "styles.css"));
        ExtractEmbeddedResource("html.script.js", Path.Combine(outputDir, "script.js"));

        // Export each page
        int totalPages = book.Pages.Count;
        var progressDialog = new ProgressDialog("Exporting to HTML");
        progressDialog.Show();

        for (int pagenum = 0; pagenum < totalPages; pagenum++) {
            if (progressDialog.IsCancelled) {
                break;
            }
            await Task.Run(() => { var x = 1;  });

            PhotoPageModel page = book.Pages[pagenum];
            string filename = Path.Combine(outputDir, $"page{pagenum}.html");
            progressDialog.UpdateProgress(pagenum + 1, totalPages, $"page{pagenum}.html");
            ExportPageToHtml(page, filename, pagenum, totalPages, imagesDir);
        }

        progressDialog.Close();
    }

    private static void ExtractEmbeddedResource(string resourceName, string outputPath) {
        var assembly = Assembly.GetExecutingAssembly();
        var fullResourceName = $"Folio.{resourceName}";

        using (Stream? resourceStream = assembly.GetManifestResourceStream(fullResourceName)) {
            if (resourceStream == null) {
                throw new Exception($"Could not find embedded resource: {fullResourceName}");
            }

            using (FileStream fileStream = File.Create(outputPath)) {
                resourceStream.CopyTo(fileStream);
            }
        }
    }

    private static string LoadEmbeddedResourceAsString(string resourceName) {
        var assembly = Assembly.GetExecutingAssembly();
        var fullResourceName = $"Folio.{resourceName}";

        using (Stream? resourceStream = assembly.GetManifestResourceStream(fullResourceName)) {
            if (resourceStream == null) {
                throw new Exception($"Could not find embedded resource: {fullResourceName}");
            }

            using (StreamReader reader = new StreamReader(resourceStream)) {
                return reader.ReadToEnd();
            }
        }
    }

    public static void ExportPageToHtml(PhotoPageModel page, string filename, int pageNum, int totalPages, string imagesDir) {
        Size pageSize = new Size(1125, 825);
        var pageContent = new System.Text.StringBuilder();
        int imageIndex = 0;

        // Special case: fullbleed template (single image fills entire page)
        if (page.TemplateName == "875x1125_32_1p1h0v0t_fb") {
            if (page.Images.Count > 0 && page.Images[0] != null) {
                var imageOrigin = page.Images[0];
                string localImagePath = CopyAndConvertImageToJpeg(imageOrigin!.SourcePath, imagesDir, pageNum, imageIndex);
                string altText = Path.GetFileNameWithoutExtension(imageOrigin.SourcePath);

                pageContent.AppendLine($"        <a href=\"{localImagePath}\" style=\"top: 0px; left: 0px; width: {pageSize.Width:F0}px; height: {pageSize.Height:F0}px;\">");
                pageContent.AppendLine($"            <img src=\"{localImagePath}\" alt=\"{altText}\">");
                pageContent.AppendLine("        </a>");
                imageIndex++;
            }
        } else {
            // Create AspectPreservingGrid from template
            var grid = PhotoPageView.APGridFromV3Template(page.TemplateName, page);
            if (grid == null) {
                Debug.WriteLine($"Unknown template: {page.TemplateName}");
                return;
            }

            // Solve layout to get positions and sizes
            var layoutResult = grid.LayoutSolution(pageSize);

            if (!layoutResult.IsValid) {
                throw new Exception($"Failed to solve layout for page {pageNum}");
            }

            // Build page content (images and captions) from grid

            // Iterate through grid children to extract images and captions
            foreach (UIElement child in grid.Children) {
                int row = Grid.GetRow(child);
                int col = Grid.GetColumn(child);
                int rowspan = Grid.GetRowSpan(child);
                int colspan = Grid.GetColumnSpan(child);

                // Calculate absolute position
                double x = layoutResult.padding.X / 2;
                for (int i = 0; i < col; i++) {
                    x += layoutResult.colSizes[i];
                }

                double y = layoutResult.padding.Y / 2;
                for (int i = 0; i < row; i++) {
                    y += layoutResult.rowSizes[i];
                }

                double width = 0;
                for (int i = col; i < col + colspan; i++) {
                    width += layoutResult.colSizes[i];
                }

                double height = 0;
                for (int i = row; i < row + rowspan; i++) {
                    height += layoutResult.rowSizes[i];
                }

                // Generate HTML based on element type
                if (child is DroppableImageDisplay imageDisplay) {
                    // Get the image index from the Grid.Row/Column to find the corresponding ImageOrigin
                    int gridImageIndex = GetImageIndexFromChild(grid, child);
                    if (gridImageIndex >= 0 && gridImageIndex < page.Images.Count && page.Images[gridImageIndex] != null) {
                        var imageOrigin = page.Images[gridImageIndex];
                        string localImagePath = CopyAndConvertImageToJpeg(imageOrigin!.SourcePath, imagesDir, pageNum, imageIndex);
                        string altText = Path.GetFileNameWithoutExtension(imageOrigin.SourcePath);

                        pageContent.AppendLine($"        <a href=\"{localImagePath}\" style=\"top: {y:F0}px; left: {x:F0}px; width: {width:F0}px; height: {height:F0}px;\">");
                        pageContent.AppendLine($"            <img src=\"{localImagePath}\" alt=\"{altText}\">");
                        pageContent.AppendLine("        </a>");
                        imageIndex++;
                    }
                } else if (child is CaptionView captionView) {
                    // Extract caption text
                    string captionText = ExtractPlainTextFromRichText(page.RichText);
                    if (!string.IsNullOrWhiteSpace(captionText)) {
                        pageContent.AppendLine($"        <div class=\"content\" style=\"position: absolute; top: {y:F0}px; left: {x:F0}px; width: {width:F0}px\">");

                        // Split on double newlines to create paragraphs
                        var paragraphs = captionText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var para in paragraphs) {
                            if (!string.IsNullOrWhiteSpace(para)) {
                                pageContent.AppendLine($"            <p>{System.Security.SecurityElement.Escape(para.Trim())}</p>");
                            }
                        }

                        pageContent.AppendLine("        </div>");
                    }
                }
            }
        }

        // Load HTML template
        string template = LoadEmbeddedResourceAsString("html.page-template.html");

        // Build navigation buttons
        string prevPageButton = "";
        if (pageNum > 0) {
            prevPageButton = $@"    <a href=""page{pageNum - 1}.html"" class=""nav-button nav-prev"">
        <svg width=""48"" height=""48"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
            <path d=""M15 18L9 12L15 6"" stroke=""white"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""/>
        </svg>
    </a>";
        }

        string nextPageButton = "";
        if (pageNum < totalPages - 1) {
            nextPageButton = $@"    <a href=""page{pageNum + 1}.html"" class=""nav-button nav-next"">
        <svg width=""48"" height=""48"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
            <path d=""M9 18L15 12L9 6"" stroke=""white"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""/>
        </svg>
    </a>";
        }

        // Replace placeholders
        string html = template
            .Replace("%insert-page-content-here%", pageContent.ToString().TrimEnd())
            .Replace("%prev-page%", prevPageButton)
            .Replace("%next-page%", nextPageButton)
            .Replace("%page-number%", pageNum.ToString());

        // Write to file
        File.WriteAllText(filename, html);
    }

    private static int GetImageIndexFromChild(AspectPreservingGrid grid, UIElement child) {
        // The image index is tracked by counting DroppableImageDisplay children before this one
        int index = 0;
        foreach (UIElement c in grid.Children) {
            if (c == child) {
                return index;
            }
            if (c is DroppableImageDisplay) {
                index++;
            }
        }
        return -1;
    }

    private static string ExtractPlainTextFromRichText(string richTextXaml) {
        if (string.IsNullOrWhiteSpace(richTextXaml)) {
            return string.Empty;
        }

        try {
            // Parse the XAML FlowDocument and extract plain text
            var flowDoc = new FlowDocument();
            using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(richTextXaml))) {
                var range = new TextRange(flowDoc.ContentStart, flowDoc.ContentEnd);
                range.Load(stream, DataFormats.Xaml);
                return range.Text;
            }
        } catch {
            // If parsing fails, return empty string
            return string.Empty;
        }
    }

    private static string CopyAndConvertImageToJpeg(string sourcePath, string imagesDir, int pageNum, int imageNum) {
        // Generate output filename
        string outputFileName = $"page{pageNum}-image{imageNum}.jpg";
        string outputPath = Path.Combine(imagesDir, outputFileName);

        // Check if source is already a JPEG
        string sourceExt = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (sourceExt == ".jpg" || sourceExt == ".jpeg") {
            // Just copy the file
            File.Copy(sourcePath, outputPath, overwrite: true);
        } else {
            // Convert to JPEG
            ConvertImageToJpeg(sourcePath, outputPath);
        }

        // Return relative path for HTML (relative to the html-output directory)
        return $"images/{outputFileName}";
    }

    private static void ConvertImageToJpeg(string sourcePath, string outputPath) {
        try {
            // Load the image
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            // Encode as JPEG
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 90;
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            // Save to file
            using (FileStream stream = new FileStream(outputPath, FileMode.Create)) {
                encoder.Save(stream);
            }
        } catch (Exception ex) {
            Debug.WriteLine($"Error converting image {sourcePath}: {ex.Message}");
            throw;
        }
    }

    public static void PrintBook(BookModel book, object dataContext) {
        string outputDir = RootControl.dbDir + @"\output";
        int totalPages = book.Pages.Count;
        var progressDialog = new ProgressDialog("Printing Book");
        progressDialog.Show();

        int pagenum = 0;
        foreach (PhotoPageModel page in book.Pages) {
            if (progressDialog.IsCancelled) {
                break;
            }

            string filename = String.Format(outputDir + @"\page-{0:D2}.jpg", pagenum);
            progressDialog.UpdateProgress(pagenum + 1, totalPages, $"page-{pagenum:D2}.jpg");
            DoWithOOMTryCatch(() => PrintPage(page, filename, dataContext));
            pagenum++;
        }

        progressDialog.Close();
    }

    private static void DoWithOOMTryCatch(Action action) {
        bool success = false;
        int retryCount = 0;
        while (!success) {
            retryCount++;
            try {
                action();
                success = true;
            } catch (OutOfMemoryException) {
                // garbage collector hasn't run recently enough to catch up with native bitmaps
                GC.Collect();
                GC.Collect(2);
                GC.WaitForPendingFinalizers();

                // UNDONE: detect infinite recursion case when we really are out of memory.
                // Experimentally, we seem to bomb out when working set hits about 1.5gb on a 32bit box
                if (retryCount > 5)
                    throw; // rethrow
            }
        }
    }

    public static void PrintPage(PhotoPageModel page, string filename, object dataContext) {
        double scaleFactor = 3;
        Size size = new Size(1125 * scaleFactor, 875 * scaleFactor);
        var target = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Default);

        var grid = new Grid { Width = size.Width, Height = size.Height };
        var p = new PhotoPageView();
        p.IsPrintMode = true;
        p.Page = page;
        p.DataContext = dataContext;
        grid.Children.Add(p);

        // to get Loaded event & databinding, need a PresentationSource
        using (var source = new HwndSource(new HwndSourceParameters()) { RootVisual = grid }) {
            grid.Measure(size);
            grid.Arrange(new Rect(size));
            grid.UpdateLayout();

            // run databinding.  Also clear out any items RichTextBox queues up, if you don't
            // you'll eventually hit OutOfMemoryException.
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            // work around ImageDisplay.UpdateImageDisplay broken if arrangeSize not available
            InvalidateMeasureRecursive(grid);

            grid.InvalidateMeasure();
            grid.Measure(size);
            grid.Arrange(new Rect(size));
            grid.UpdateLayout();

            // run databinding.  Also clear out any items RichTextBox queues up, if you don't
            // you'll eventually hit OutOfMemoryException.
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            target.Render(grid);
        }

        var encoder = new JpegBitmapEncoder();
        encoder.QualityLevel = 100; // max
        encoder.Frames.Add(BitmapFrame.Create(target));
        using (Stream s = File.Create(filename)) {
            encoder.Save(s);
        }
    }

    private static void InvalidateMeasureRecursive(DependencyObject element) {
        if (element == null)
            return;

        // Call InvalidateMeasure if this is a UIElement
        if (element is UIElement uiElement) {
            uiElement.InvalidateMeasure();
        }

        // Recursively process all visual children
        int childCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childCount; i++) {
            DependencyObject child = VisualTreeHelper.GetChild(element, i);
            InvalidateMeasureRecursive(child);
        }
    }
}
