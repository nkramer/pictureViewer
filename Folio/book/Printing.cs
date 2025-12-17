using Folio.Core;
using Folio.Shell;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Folio.Book;

public static class Printing {
    public static void ExportBookToHtml(BookModel book) {
        string outputDir = RootControl.dbDir + @"\html-output";

        // Create output directory if it doesn't exist
        if (!Directory.Exists(outputDir)) {
            Directory.CreateDirectory(outputDir);
        }

        // Get the solution directory (where the html folder is)
        string solutionDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.FullName;
        string htmlSourceDir = Path.Combine(solutionDir, "html");

        // Copy CSS and JS files to output directory
        File.Copy(Path.Combine(htmlSourceDir, "styles.css"), Path.Combine(outputDir, "styles.css"), overwrite: true);
        File.Copy(Path.Combine(htmlSourceDir, "script.js"), Path.Combine(outputDir, "script.js"), overwrite: true);

        // Export each page
        int totalPages = book.Pages.Count;
        for (int pagenum = 0; pagenum < totalPages; pagenum++) {
            PhotoPageModel page = book.Pages[pagenum];
            string filename = Path.Combine(outputDir, $"page{pagenum}.html");
            ExportPageToHtml(page, filename, pagenum, totalPages);
        }
    }

    public static void ExportPageToHtml(PhotoPageModel page, string filename, int pageNum, int totalPages) {
        // Create AspectPreservingGrid from template
        var grid = PhotoPageView.APGridFromV3Template(page.TemplateName, page);
        if (grid == null) {
            return;
        }

        // Solve layout to get positions and sizes
        Size pageSize = new Size(1125, 825);
        var layoutResult = grid.LayoutSolution(pageSize);

        if (!layoutResult.IsValid) {
            throw new Exception($"Failed to solve layout for page {pageNum}");
        }

        // Build HTML content
        var html = new System.Text.StringBuilder();

        // HTML header
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>Photo Book - Page {pageNum}</title>");
        html.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        html.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
        html.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@300;400;600&display=swap\" rel=\"stylesheet\">");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"styles.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        // Navigation buttons
        if (pageNum > 0) {
            html.AppendLine($"    <a href=\"page{pageNum - 1}.html\" class=\"nav-button nav-prev\">");
            html.AppendLine("        <svg width=\"48\" height=\"48\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\">");
            html.AppendLine("            <path d=\"M15 18L9 12L15 6\" stroke=\"white\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
            html.AppendLine("        </svg>");
            html.AppendLine("    </a>");
        }

        if (pageNum < totalPages - 1) {
            html.AppendLine($"    <a href=\"page{pageNum + 1}.html\" class=\"nav-button nav-next\">");
            html.AppendLine("        <svg width=\"48\" height=\"48\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\">");
            html.AppendLine("            <path d=\"M9 18L15 12L9 6\" stroke=\"white\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
            html.AppendLine("        </svg>");
            html.AppendLine("    </a>");
        }

        html.AppendLine();
        html.AppendLine("    <div class=\"container\">");

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
                int imageIndex = GetImageIndexFromChild(grid, child);
                if (imageIndex >= 0 && imageIndex < page.Images.Count && page.Images[imageIndex] != null) {
                    var imageOrigin = page.Images[imageIndex];
                    string imagePath = imageOrigin!.SourcePath;
                    string altText = Path.GetFileNameWithoutExtension(imagePath);

                    html.AppendLine($"        <a href=\"{imagePath}\" style=\"top: {y:F0}px; left: {x:F0}px; width: {width:F0}px; height: {height:F0}px;\">");
                    html.AppendLine($"            <img src=\"{imagePath}\" alt=\"{altText}\">");
                    html.AppendLine("        </a>");
                }
            } else if (child is CaptionView captionView) {
                // Extract caption text
                string captionText = ExtractPlainTextFromRichText(page.RichText);
                if (!string.IsNullOrWhiteSpace(captionText)) {
                    html.AppendLine($"        <div class=\"content\" style=\"position: absolute; top: {y:F0}px; left: {x:F0}px; width: {width:F0}px\">");

                    // Split on double newlines to create paragraphs
                    var paragraphs = captionText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var para in paragraphs) {
                        if (!string.IsNullOrWhiteSpace(para)) {
                            html.AppendLine($"            <p>{System.Security.SecurityElement.Escape(para.Trim())}</p>");
                        }
                    }

                    html.AppendLine("        </div>");
                }
            }
        }

        html.AppendLine("    </div>");
        html.AppendLine();
        html.AppendLine("    <script src=\"script.js\"></script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        // Write to file
        File.WriteAllText(filename, html.ToString());
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

    public static void PrintBook(BookModel book, object dataContext) {
        string outputDir = RootControl.dbDir + @"\output";
        int pagenum = 0;
        foreach (PhotoPageModel page in book.Pages) {
            string filename = String.Format(outputDir + @"\page-{0:D2}.jpg", pagenum);
            DoWithOOMTryCatch(() => PrintPage(page, filename, dataContext));
            pagenum++;
        }
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
