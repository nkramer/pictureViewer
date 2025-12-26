using FluentAssertions;
using Folio.Book;
using Folio.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Folio.Tests.Book;
public class AspectPreservingGridTests {
    private readonly ITestOutputHelper _output;

    public AspectPreservingGridTests(ITestOutputHelper output) {
        _output = output;
    }

    // Captures the position and size of each child element
    public class ChildElementLayout {
        public int ChildIndex { get; set; }
        public string ChildType { get; set; } = null!;
        public int Row { get; set; }
        public int Column { get; set; }
        public int RowSpan { get; set; }
        public int ColumnSpan { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Aspect { get; set; } = null!;

        public override string ToString() {
            return $"[{ChildIndex}] {ChildType} ({Aspect}) @ R{Row}C{Column} (span {RowSpan}x{ColumnSpan}): " +
                   $"Pos=({X:F2},{Y:F2}) Size=({Width:F2}x{Height:F2})";
        }
    }

    // Captures the complete layout for a template
    public class TemplateLayout {
        public string TemplateName { get; set; } = null!;
        public double ContainerWidth { get; set; }
        public double ContainerHeight { get; set; }
        public double[] RowSizes { get; set; } = null!;
        public double[] ColumnSizes { get; set; } = null!;
        public double PaddingX { get; set; }
        public double PaddingY { get; set; }
        public List<ChildElementLayout> Children { get; set; } = new List<ChildElementLayout>();

        public string ToDetailedString() {
            var sb = new StringBuilder();
            sb.AppendLine($"Template: {TemplateName}");
            sb.AppendLine($"Container: {ContainerWidth}x{ContainerHeight}");
            sb.AppendLine($"Padding: ({PaddingX:F2}, {PaddingY:F2})");
            sb.AppendLine($"Row Sizes: [{string.Join(", ", RowSizes.Select(r => $"{r:F2}"))}]");
            sb.AppendLine($"Column Sizes: [{string.Join(", ", ColumnSizes.Select(c => $"{c:F2}"))}]");
            sb.AppendLine($"Children ({Children.Count}):");
            foreach (var child in Children) {
                sb.AppendLine($"  {child}");
            }
            return sb.ToString();
        }
    }

    [Fact]
    public void AllTemplates_1125x875() {
        ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(1125, 875);
    }

    [Fact]
    public void AllTemplates_1336x768() {
        ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(1336, 768);
    }

    [Fact]
    public void AllTemplates_1920x1080() {
        ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(1920, 1080);
    }

    //[Fact]
    //public void AllTemplates_2920x1080() {
    //    ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(2920, 1080);
    //}

    [Fact]
    public void AllTemplates_875x1125() {
        ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(875, 1125);
    }

    //[Fact]
    //public void AllTemplates_768x1336() {
    //    ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(768, 1336);
    //}

    //[Fact]
    //public void AllTemplates_1080x1920() {
    //    ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(1080, 1920);
    //}

    private void ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing(int width, int height) {
        var failures = new System.Collections.Generic.List<string>();
        var successes = new System.Collections.Generic.List<string>();
        Exception? setupException = null;
        var thread = new Thread(() => {
            try {
                WpfTestHelper.EnsureApplicationInitialized();
                var templateNames = PhotoPageView.GetAllTemplateNames().ToList();
                templateNames.Should().NotBeEmpty("there should be at least one template");
                foreach (var templateName in templateNames) {
                    try {
                        var bookModel = new BookModel();
                        var pageModel = new PhotoPageModel(bookModel) { TemplateName = templateName };
                        var grid = PhotoPageView.APGridFromV3Template(templateName, pageModel);
                        if (grid != null) {
                            var sizes = grid.LayoutSolution(new Size(width, height));
                            if (!sizes.IsValid) {
                                failures.Add($"{templateName}: layout failure {sizes.error}");
                            }
                        }
                    } catch (Exception ex) {
                        failures.Add($"{templateName}: {ex.Message}");
                    }
                }
            } catch (Exception ex) {
                setupException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (setupException != null) {
            throw setupException;
        }
        if (failures.Count > 0) {
            var failureReport = $"Failed templates ({failures.Count}/{failures.Count + successes.Count}):\n" +
                              string.Join("\n", failures) +
                              $"\n\nSucceeded ({successes.Count}/{failures.Count + successes.Count}):\n" +
                              string.Join("\n", successes);
            _output.WriteLine(failureReport);
            Assert.Fail(failureReport);
        }
    }

    [Fact]
    public void CaptureChildElementSizes_AllTemplates_1920x1080() {
        CaptureChildElementSizesForAllTemplates(1920, 1080);
    }

    [Fact]
    public void CaptureChildElementSizes_AllTemplates_1125x875() {
        CaptureChildElementSizesForAllTemplates(1125, 875);
    }

    [Fact]
    public void CaptureChildElementSizes_AllTemplates_875x1125() {
        CaptureChildElementSizesForAllTemplates(875, 1125);
    }

    [Fact]
    public void CaptureChildElementSizes_AllTemplates_1336x768() {
        CaptureChildElementSizesForAllTemplates(1336, 768);
    }

    private void CaptureChildElementSizesForAllTemplates(int width, int height) {
        var layouts = new List<TemplateLayout>();
        Exception? setupException = null;

        var thread = new Thread(() => {
            try {
                WpfTestHelper.EnsureApplicationInitialized();

                var templateNames = PhotoPageView.GetAllTemplateNames().ToList();
                templateNames.Should().NotBeEmpty("there should be at least one template");

                foreach (var templateName in templateNames) {
                    var bookModel = new BookModel();
                    var pageModel = new PhotoPageModel(bookModel) { TemplateName = templateName };
                    var grid = PhotoPageView.APGridFromV3Template(templateName, pageModel);

                    if (grid != null) {
                        var sizes = grid.LayoutSolution(new Size(width, height));
                        if (sizes.IsValid) {
                            var layout = CaptureLayout(templateName, grid, sizes, width, height);
                            layouts.Add(layout);
                        }
                    }
                }
            } catch (Exception ex) {
                setupException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (setupException != null) {
            throw setupException;
        }

        // Verify we captured at least some layouts
        layouts.Should().NotBeEmpty("should have captured at least one template layout");

        // Build the baseline output
        var sb = new StringBuilder();
        sb.AppendLine($"=== Captured layouts for {width}x{height} ===");
        sb.AppendLine($"Total templates: {layouts.Count}");
        sb.AppendLine("");

        foreach (var layout in layouts) {
            sb.AppendLine(layout.ToDetailedString());
            sb.AppendLine("");
        }

        // Write to test output
        _output.WriteLine(sb.ToString());

        // Save to baseline file
        var baselineDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Baselines");
        Directory.CreateDirectory(baselineDir);
        var baselineFile = Path.Combine(baselineDir, $"AspectPreservingGrid_{width}x{height}.txt");
        File.WriteAllText(baselineFile, sb.ToString());
        _output.WriteLine($"Baseline saved to: {baselineFile}");
    }

    private TemplateLayout CaptureLayout(string templateName, AspectPreservingGrid grid,
        AspectPreservingGrid.LayoutResult sizes, double containerWidth, double containerHeight) {
        var layout = new TemplateLayout {
            TemplateName = templateName,
            ContainerWidth = containerWidth,
            ContainerHeight = containerHeight,
            RowSizes = sizes.rowSizes,
            ColumnSizes = sizes.colSizes,
            PaddingX = sizes.padding.X,
            PaddingY = sizes.padding.Y
        };

        // Calculate the position and size of each child element
        for (int childIndex = 0; childIndex < grid.Children.Count; childIndex++) {
            var child = grid.Children[childIndex];
            int row = Grid.GetRow(child);
            int col = Grid.GetColumn(child);
            int rowspan = Grid.GetRowSpan(child);
            int colspan = Grid.GetColumnSpan(child);

            // Calculate X position (sum of column widths before this column)
            double x = 0;
            for (int i = 0; i < col; i++) {
                x += sizes.colSizes[i];
            }

            // Calculate Y position (sum of row heights before this row)
            double y = 0;
            for (int i = 0; i < row; i++) {
                y += sizes.rowSizes[i];
            }

            // Calculate width (sum of spanned column widths)
            double childWidth = 0;
            for (int i = col; i < col + colspan; i++) {
                childWidth += sizes.colSizes[i];
            }

            // Calculate height (sum of spanned row heights)
            double childHeight = 0;
            for (int i = row; i < row + rowspan; i++) {
                childHeight += sizes.rowSizes[i];
            }

            var childLayout = new ChildElementLayout {
                ChildIndex = childIndex,
                ChildType = GetChildTypeName(child),
                Row = row,
                Column = col,
                RowSpan = rowspan,
                ColumnSpan = colspan,
                X = x,
                Y = y,
                Width = childWidth,
                Height = childHeight,
                Aspect = AspectPreservingGrid.GetDesiredAspectRatio(child).ToString()
            };

            layout.Children.Add(childLayout);
        }

        return layout;
    }

    private string GetChildTypeName(UIElement child) {
        if (child is CaptionView)
            return "CaptionView";
        else if (child is DroppableImageDisplay)
            return "DroppableImageDisplay";
        else if (child is Border)
            return "Border";
        else
            return child.GetType().Name;
    }

    [Fact]
    public void Template_6p0h6v0t_WithMixedAspectRatios_ShouldFallbackAndSetErrorState() {
        Exception? measureException = null;
        bool errorStateSet = false;

        var thread = new Thread(() => {
            try {
                WpfTestHelper.EnsureApplicationInitialized();

                var bookModel = new BookModel();
                var pageModel = new PhotoPageModel(bookModel) { TemplateName = "875x1125_32_6p0h6v0t" };
                var grid = PhotoPageView.APGridFromV3Template("875x1125_32_6p0h6v0t", pageModel);
                grid.Should().NotBeNull("template should exist");

                // Set aspect ratios: first image is 4:3 landscape, rest are 3:2 landscape
                for (int i = 0; i < grid!.Children.Count; i++) {
                    var child = grid.Children[i];
                    if (child is DroppableImageDisplay) {
                        if (i == 0) {
                            // First spot: 4:3 aspect ratio
                            AspectPreservingGrid.SetDesiredAspectRatio(child, new Ratio(4, 3));
                        } else {
                            // Rest: 3:2 aspect ratio
                            AspectPreservingGrid.SetDesiredAspectRatio(child, new Ratio(3, 2));
                        }
                    }
                }

                // Verify ErrorState is initially false
                pageModel.ErrorState.Should().BeFalse("ErrorState should be false before layout");

                // Call Measure to trigger the layout fallback logic
                try {
                    grid.Measure(new Size(1125, 875));
                } catch (Exception ex) {
                    // If measure throws an exception, the fallback didn't work
                    measureException = ex;
                }

                // Check if ErrorState was set by the fallback logic
                errorStateSet = pageModel.ErrorState;
            } catch (Exception ex) {
                throw new Exception("Unexpected exception during test setup", ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        // Assert that Measure didn't throw an exception (fallback should handle it)
        measureException.Should().BeNull(
            "Measure should not throw an exception when using fallback layout");

        // Assert that ErrorState was set to indicate there was a problem
        errorStateSet.Should().BeTrue(
            "ErrorState should be set when layout falls back to default aspect ratios");

        _output.WriteLine($"Fallback layout succeeded with ErrorState set to: {errorStateSet}");
    }

    [Fact]
    public void GridSizesToTemplateString_ShouldGenerateTemplateFormat() {
        Exception? testException = null;
        string? templateString = null;

        var thread = new Thread(() => {
            try {
                WpfTestHelper.EnsureApplicationInitialized();

                var bookModel = new BookModel();
                var pageModel = new PhotoPageModel(bookModel) { TemplateName = "875x1125_32_1p1h0v1t" };
                var grid = PhotoPageView.APGridFromV3Template("875x1125_32_1p1h0v1t", pageModel);
                grid.Should().NotBeNull("template should exist");

                var sizes = grid!.LayoutSolution(new Size(1125, 875));
                sizes.Should().NotBeNull("ComputeSizes should return a result");
                sizes!.IsValid.Should().BeTrue("Layout should be valid");

                templateString = AspectPreservingGrid.GridSizesToTemplateString(sizes, grid);
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

        templateString.Should().NotBeNullOrEmpty("GridSizesToTemplateString should return a non-empty string");
        _output.WriteLine("Template string format:");
        _output.WriteLine(templateString);

        var lines = templateString!.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        lines.Should().NotBeEmpty("Template string should have at least one line");
        lines.Length.Should().BeGreaterThan(1, "Template string should have multiple lines (header + rows)");
    }
}
