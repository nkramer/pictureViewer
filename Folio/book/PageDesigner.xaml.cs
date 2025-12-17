using Folio.Core;
using Folio.Library;
using Folio.Shell;
using Folio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Folio.Book;
// Represents a book entry in the book selector dropdown
public record BookInfo(string DisplayName, string FilePath, bool IsNewBook) {
    public override string ToString() {
        return DisplayName;
    }
}

public partial class PageDesigner : UserControl, INotifyPropertyChanged, IScreen {
    private CommandHelper commands;
    private BookModel book = null!;// Initialized in constructor 
    private bool twoPageMode = false;
    private UndoRedoManager undoRedoManager;
    private bool isLoadingBook = false; // Flag to prevent recursive loading

    // HACK: seems easier to implement INotifyPropertyChanged than make everything a dependency property
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetTwoPageMode(bool mode) {
        twoPageMode = mode;
        tableOfContentsListbox.ItemsSource = (twoPageMode)
            ? book.TwoPages as System.Collections.IEnumerable
            : book.Pages as System.Collections.IEnumerable;
        pageview.Visibility = (!twoPageMode) ? Visibility.Visible : Visibility.Collapsed;
        twopageview.Visibility = (twoPageMode) ? Visibility.Visible : Visibility.Collapsed;
    }

    public PageDesigner() {
        InitializeComponent();

        if (RootControl.Instance!.book == null) {
            // First time opening PageDesigner - load default book
            RootControl.Instance.currentBookPath = RootControl.dbDir + @"\testPhotoBook.xml";
            RootControl.Instance.book = BookModel.Load(RootControl.Instance.currentBookPath);
            RootControl.Instance.book.SelectedPage = RootControl.Instance.book.Pages[0];
        } else if (RootControl.Instance.currentBookPath == null) {
            Debug.Fail("WTF? A book with no path?");
            // Book exists but path wasn't tracked - default to testPhotoBook.xml
            RootControl.Instance.currentBookPath = RootControl.dbDir + @"\testPhotoBook.xml";
        }

        book = RootControl.Instance.book!;
        book.PropertyChanged += new PropertyChangedEventHandler(book_PropertyChanged!); // BUG?: never unhooked
        book.ImagesChanged += new EventHandler(book_ImagesChanged!);

        this.DataContext = book;
        SetTwoPageMode(false);

        // Initialize undo/redo manager
        undoRedoManager = new UndoRedoManager(
            createSnapshot: () => book.Serialize(),
            restoreSnapshot: (xml) => LoadBookFromXml(xml)
        );

        this.commands = new CommandHelper(this);
        // Set the snapshot callback once for all commands
        this.commands.RecordSnapshot = () => undoRedoManager.RecordSnapshot();
        this.commands.contextmenu = new ContextMenu();
        this.ContextMenu = this.commands.contextmenu;
        CreateCommands();

        var p = new PhotoGrid(RootControl.Instance);
        p.MaxPhotosToDisplay = 80; // hack, should calculate by window size
        p.Background = Brushes.Transparent;
        p.Mode = PhotoGridMode.Designer;
        photogridHolder.Child = p;
        p.filters.Visibility = Visibility.Collapsed;

        tableOfContentsListbox.SelectedItem = book.SelectedPage;

        this.Loaded += new RoutedEventHandler(PageDesigner_Loaded);
        this.Focusable = true;
        this.KeyDown += new KeyEventHandler(PageDesigner_KeyDown);
        this.LostFocus += new RoutedEventHandler(PageDesigner_LostFocus);

        bookSelector.Items.Clear(); // remmove design-time item
        // Populate the book selector dropdown
        PopulateBookSelector();
    }

    private void PopulateBookSelector() {
        var bookList = new List<BookInfo>();

        // Add "New Book" entry at the top
        bookList.Add(new BookInfo("New Book...", FilePath: "", IsNewBook: true));

        // Get all .xml files from dbDir
        if (Directory.Exists(RootControl.dbDir)) {
            var xmlFiles = Directory.GetFiles(RootControl.dbDir, "*.xml");
            foreach (var filePath in xmlFiles.OrderBy(f => Path.GetFileNameWithoutExtension(f))) {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                bookList.Add(new BookInfo(fileName, filePath, IsNewBook: false));
            }
        }

        bookSelector.ItemsSource = bookList;

        // Select the current book if it exists
        if (RootControl.Instance!.book != null && RootControl.Instance.currentBookPath != null) {
            var currentBook = bookList.FirstOrDefault(b => b.FilePath == RootControl.Instance.currentBookPath);
            if (currentBook != null) {
                bookSelector.SelectedItem = currentBook;
            }
        }
    }

    private void BookSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (isLoadingBook || bookSelector.SelectedItem == null) {
            return;
        }

        var selectedBook = (BookInfo)bookSelector.SelectedItem;

        if (selectedBook.IsNewBook) {
            CreateNewBook();
        } else {
            LoadBookFromFile(selectedBook.FilePath!);
        }
    }

    private void CreateNewBook() {
        var questionWindow = new QuestionWindow();
        questionWindow.DialogTitle = "Enter name for new book:";
        questionWindow.Result = "NewBook";

        if (questionWindow.ShowDialog() == true) {
            var bookName = questionWindow.Result;
            if (!string.IsNullOrWhiteSpace(bookName)) {
                // Create a new book
                var newBook = new BookModel();
                // Add a blank first page
                newBook.Pages.Add(new PhotoPageModel(newBook));
                newBook.SelectedPage = newBook.Pages[0];

                // Save the new book
                var filePath = Path.Combine(RootControl.dbDir, bookName + ".xml");
                newBook.Save(filePath);

                // Load the new book
                LoadBookFromFile(filePath);

                // Refresh the book selector
                isLoadingBook = true;
                PopulateBookSelector();
                var newBookInfo = ((List<BookInfo>)bookSelector.ItemsSource)
                    .FirstOrDefault(b => b.FilePath == filePath);
                if (newBookInfo != null) {
                    bookSelector.SelectedItem = newBookInfo;
                }
                isLoadingBook = false;
            } else {
                // User entered empty name, revert selection
                isLoadingBook = true;
                RevertBookSelection();
                isLoadingBook = false;
            }
        } else {
            // User cancelled, revert selection
            isLoadingBook = true;
            RevertBookSelection();
            isLoadingBook = false;
        }
    }

    // Loads book from XML string - the core implementation
    private void LoadBookFromXml(string xmlString) {
        var newBook = BookModel.Parse(xmlString);
        isLoadingBook = true;

        // Unhook events from old book if needed
        if (book != null) {
            book.PropertyChanged -= book_PropertyChanged!;
            book.ImagesChanged -= book_ImagesChanged!;
        }

        // Remember selected page index to preserve it
        int preservedPageIndex = -1;
        if (book != null && book.SelectedPage != null) {
            preservedPageIndex = book.Pages.IndexOf(book.SelectedPage);
        }

        // Update the book reference (don't update currentBookPath here)
        this.book = newBook;
        RootControl.Instance!.book = newBook;

        // Hook up events to new book
        book.PropertyChanged += book_PropertyChanged!;
        book.ImagesChanged += book_ImagesChanged!;

        // Update the data context
        this.DataContext = book;

        // Update the table of contents
        SetTwoPageMode(twoPageMode);
        tableOfContentsListbox.SelectedItem = book.SelectedPage;

        // Restore selection
        if (newBook.Pages.Count > 0) {
            if (preservedPageIndex >= 0 && preservedPageIndex < newBook.Pages.Count) {
                newBook.SelectedPage = newBook.Pages[preservedPageIndex];
            } else {
                newBook.SelectedPage = newBook.Pages[0];
            }
        } else {
            // Add a blank page if the book is empty
            newBook.Pages.Add(new PhotoPageModel(newBook));
            newBook.SelectedPage = newBook.Pages[0];
        }
        tableOfContentsListbox.SelectedItem = book.SelectedPage;

        this.Focus();
    }

    // Loads book from file - reads file and calls LoadBookFromXml
    private void LoadBookFromFile(string filePath) {
        if (filePath == RootControl.Instance!.currentBookPath)
            return; // Already loaded, if we reload it'll stomp any changes made in memory

        if (!File.Exists(filePath)) {
            ThemedMessageBox.Show($"Book file not found: {filePath}");
            return;
        }

        try {
            isLoadingBook = true;
            var xmlString = File.ReadAllText(filePath);
            LoadBookFromXml(xmlString);
            RootControl.Instance.currentBookPath = filePath;
        } catch (Exception ex) {
            ThemedMessageBox.Show($"Error loading book: {ex.Message}");
        } finally {
            isLoadingBook = false;
        }
    }

    private void RevertBookSelection() {
        // Revert to the currently loaded book
        if (book != null && RootControl.Instance!.currentBookPath != null) {
            var currentBook = ((List<BookInfo>)bookSelector.ItemsSource)
                .FirstOrDefault(b => b.FilePath == RootControl.Instance.currentBookPath);
            if (currentBook != null) {
                bookSelector.SelectedItem = currentBook;
            }
        }
    }

    void book_ImagesChanged(object? sender, EventArgs e) {
        RootControl.Instance!.UpdateCache();
    }

    void book_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        // todo: handle case where RootControl.book changes -- unhook listener, etc.
        if (e.PropertyName == "" || e.PropertyName == "SelectedPage") {
            RootControl.Instance!.UpdateCache();
        }
    }

    void PageDesigner_LostFocus(object sender, RoutedEventArgs e) {

    }

    private ScrollViewer GetTOCScrollViewer() {
        // hardcoding to the listbox template
        var border = VisualTreeHelper.GetChild(tableOfContentsListbox, 0);
        Debug.Assert(border is Border);
        var borderchild = VisualTreeHelper.GetChild(border, 0);
        Debug.Assert(borderchild is ScrollViewer);
        ScrollViewer sv = (ScrollViewer)borderchild;
        return sv;
    }

    private void WorkInProgress() {
        ScrollViewer sv = GetTOCScrollViewer();
        int firstVisibleItem = (int)sv.VerticalOffset;
        // returns item # not pixel count in our case
    }

    protected void NotifyPropertyChanged(String info) {
        if (PropertyChanged != null) {
            var args = new PropertyChangedEventArgs(info);
            PropertyChanged(this, args);
        }
    }

    void PageDesigner_KeyDown(object sender, KeyEventArgs e) {
        if (e.OriginalSource is RichTextBox && e.Key == Key.Escape) {
            this.Focus();
        }
    }

    private void TemplateName(DataTemplate t) {
    }

    void PageDesigner_Loaded(object sender, RoutedEventArgs e) {
        bool res = this.Focus();
    }

    // code for after layout, but before Loaded & data binding
    protected override Size ArrangeOverride(Size arrangeBounds) {
        var result = base.ArrangeOverride(arrangeBounds);
        if (tableOfContentsListbox.Visibility == Visibility.Visible) {
            this.GetTOCScrollViewer().ScrollChanged += new ScrollChangedEventHandler(PageDesigner_ScrollChanged);
        }
        RootControl.Instance!.loader.SetTargetSize((int)pageholder.ActualWidth * 2, (int)pageholder.ActualHeight * 2);
        RootControl.Instance.loader.PrefetchPolicy = PrefetchPolicy.PageDesigner;
        return result;
    }


    void PageDesigner_ScrollChanged(object sender, ScrollChangedEventArgs e) {
        ScrollViewer sv = GetTOCScrollViewer();
        int firstVisibleItem = (int)sv.VerticalOffset;
    }

    private void CreateCommands() {
        Command command;

        // Undo command (Ctrl-Z)
        command = new Command();
        command.Key = Key.Z;
        command.ModifierKeys = ModifierKeys.Control;
        command.Text = "Undo";
        command.Execute += delegate () {
            undoRedoManager.Undo();
        };
        commands.AddCommand(command);

        // Redo command (Ctrl-Y)
        command = new Command();
        command.Key = Key.Y;
        command.ModifierKeys = ModifierKeys.Control;
        command.Text = "Redo";
        command.Execute += delegate () {
            undoRedoManager.Redo();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.W;
        command.Text = "Save database (write)";
        command.Execute += delegate () {
            book.Save(RootControl.Instance!.currentBookPath!);
            book.Save(RootControl.dbDirCopy + @"\" + Path.GetFileName(RootControl.Instance.currentBookPath));
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.O;
        command.Text = "Open book";
        command.Execute += delegate () {
            bookSelector.Focus();
            bookSelector.IsDropDownOpen = true;
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.P;
        command.ModifierKeys = ModifierKeys.Shift;
        command.Text = "Print";
        command.Execute += delegate () {
            PrintBook();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.H;
        command.ModifierKeys = ModifierKeys.Shift;
        command.Text = "Export HTML";
        command.Execute += delegate () {
            ExportBookToHtml();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.F;
        command.Text = "Flip";
        command.ShouldRecordSnapshot = true;
        command.Execute += delegate () {
            if (SelectedPage != null)
                SelectedPage.Flipped = !SelectedPage.Flipped;
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.B;
        command.Text = "Background";
        command.ShouldRecordSnapshot = true;
        command.Execute += delegate () {
            if (SelectedPage != null) {
                var fg = SelectedPage.ForegroundColor;
                var bg = SelectedPage.BackgroundColor;
                SelectedPage.BackgroundColor = fg;
                SelectedPage.ForegroundColor = bg;
            }
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.F11;
        command.Text = "Fullscreen";
        command.Execute += delegate () {
            RootControl.Instance!.PushScreen(new BookViewerFullscreen());
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.T;
        command.Text = "Choose template";
        command.Execute += delegate () {
            ShowTemplateChooser();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.M;
        command.Text = "New page";
        command.ShouldRecordSnapshot = true;
        command.Execute += delegate () {
            var page = new PhotoPageModel(book);
            book.Pages.Insert(tableOfContentsListbox.SelectedIndex + 1, page);
            tableOfContentsListbox.SelectedItem = page;
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.C;
        command.Text = "Copy page";
        command.Execute += delegate () {
            if (SelectedPage != null) {
                var page = SelectedPage.Clone();
                book.Pages.Insert(tableOfContentsListbox.SelectedIndex, page);
                tableOfContentsListbox.SelectedItem = page;
            }
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.D;
        command.Text = "Double pageview";
        command.Execute += delegate () {
            SetTwoPageMode(!twoPageMode);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.Delete;
        command.Text = "Delete page";
        command.ShouldRecordSnapshot = true;
        command.Execute += delegate () {
            int i = tableOfContentsListbox.SelectedIndex;
            book.Pages.Remove((tableOfContentsListbox.SelectedItem as PhotoPageModel)!);
            tableOfContentsListbox.SelectedIndex = Math.Min(i, book.Pages.Count - 1);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Next page";
        command.HasMenuItem = false;
        command.Key = Key.Right;
        command.Execute += delegate () {
            NextPage(1);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Previous page";
        command.HasMenuItem = false;
        command.Key = Key.Left;
        command.Execute += delegate () {
            NextPage(-1);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Next page";
        command.HasMenuItem = false;
        command.Key = Key.Down;
        command.HasMenuItem = false;
        command.Execute += delegate () {
            NextPage(1);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Previous page";
        command.HasMenuItem = false;
        command.Key = Key.Up;
        command.HasMenuItem = false;
        command.Execute += delegate () {
            NextPage(-1);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Forward 1 page";
        command.HasMenuItem = false;
        command.Key = Key.PageDown;
        command.DisplayKey = "PageDown";
        command.Execute += delegate () {
            NextPage(1);
        };
        commands.AddCommand(command);
#if WPF
        commands.AddBinding(command, MediaCommands.FastForward);
        commands.AddBinding(command, MediaCommands.NextTrack);
#endif

        command = new Command();
        command.Text = "Backward 1 page";
        command.HasMenuItem = false;
        command.Key = Key.PageUp;
        command.Execute += delegate () {
            NextPage(-1);
        };
        commands.AddCommand(command);
#if WPF
        commands.AddBinding(command, MediaCommands.Rewind);
        commands.AddBinding(command, MediaCommands.PreviousTrack);
#endif

        command = new Command();
        command.Text = "First page";
        command.HasMenuItem = false;
        command.Key = Key.Home;
        command.Execute += delegate () {
            tableOfContentsListbox.SelectedIndex = 0;
            tableOfContentsListbox.ScrollIntoView(tableOfContentsListbox.SelectedItem);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Last page";
        command.HasMenuItem = false;
        command.Key = Key.End;
        command.Execute += delegate () {
            tableOfContentsListbox.SelectedIndex = book.Pages.Count - 1;
            tableOfContentsListbox.ScrollIntoView(tableOfContentsListbox.SelectedItem);
        };
        commands.AddCommand(command);
    }

    public void ShowTemplateChooser() {
        var dialog = new TemplateChooserDialog(this.SelectedPage!, book);
        bool? result = dialog.ShowDialog();

        if (result == true && dialog.SelectedTemplateName != null) {
            SelectedPage!.TemplateName = dialog.SelectedTemplateName;
        }

        this.Focus();
    }

    public void PrintBook() {
        string outputDir = RootControl.dbDir + @"\output";
        int pagenum = 0;
        foreach (PhotoPageModel page in book.Pages) {
            string filename = String.Format(outputDir + @"\page-{0:D2}.jpg", pagenum);
            DoWithOOMTryCatch(() => PrintPage(page, filename, this.DataContext));
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
        //double scaleFactor = 1; // todo: = 3
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
            Debug.WriteLine("---------------------------------1");
            grid.Measure(size);
            grid.Arrange(new Rect(size));
            grid.UpdateLayout();

            // run databinding.  Also clear out any items RichTextBox queues up, if you don't
            // you'll eventually hit OutOfMemoryException.
            //Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(() => { }));
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            // work around ImageDisplay.UpdateImageDisplay broken if arrangeSize not available
            InvalidateMeasureRecursive(grid);

            Debug.WriteLine("---------------------------------2");
            grid.InvalidateMeasure();
            grid.Measure(size);
            grid.Arrange(new Rect(size));
            grid.UpdateLayout();

            // run databinding.  Also clear out any items RichTextBox queues up, if you don't
            // you'll eventually hit OutOfMemoryException.
            //Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(() => { }));
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

    public void ExportBookToHtml() {
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
            ExportPageToHtml(page, filename, pagenum, totalPages, this.DataContext);
        }
    }

    public static void ExportPageToHtml(PhotoPageModel page, string filename, int pageNum, int totalPages, object dataContext) {
        // Create AspectPreservingGrid from template
        var grid = PhotoPageView.APGridFromV3Template(page.TemplateName, page);
        if (grid == null) {
            throw new Exception($"Failed to create grid for template {page.TemplateName}");
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
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(richTextXaml))) {
                var range = new System.Windows.Documents.TextRange(flowDoc.ContentStart, flowDoc.ContentEnd);
                range.Load(stream, System.Windows.DataFormats.Xaml);
                return range.Text;
            }
        } catch {
            // If parsing fails, return empty string
            return string.Empty;
        }
    }

    private void NextPage(int increment) {
        tableOfContentsListbox.SelectedIndex = Math.Max(0,
            Math.Min(tableOfContentsListbox.SelectedIndex + increment, book.Pages.Count - 1));
        tableOfContentsListbox.ScrollIntoView(tableOfContentsListbox.SelectedItem);
    }

    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (tableOfContentsListbox.SelectedItem is PhotoPageModel) {
            SelectedPage = (PhotoPageModel)tableOfContentsListbox.SelectedItem;
        } else if (tableOfContentsListbox.SelectedItem is TwoPages) {
            TwoPages t = (TwoPages)tableOfContentsListbox.SelectedItem;
            if (t.Left != null)
                SelectedPage = t.Left;
            else
                SelectedPage = t.Right!;
        } else {
            Debug.Fail("Selection is neither PhotoPageModel nor TwoPages?");
        }
    }

    private PhotoPageModel? SelectedPage {
        get {
            return book.SelectedPage;
        }
        set {
            book.SelectedPage = value;
        }
    }

    private void listboxitem_Drop(object sender, DragEventArgs e) {
        if (e.Data.GetDataPresent(typeof(PhotoPageModel))) {
            PhotoPageModel? moving = e.Data.GetData(typeof(PhotoPageModel)) as PhotoPageModel;
            FrameworkElement droppedOnElt = (FrameworkElement)sender;
            PhotoPageModel droppedOn = (PhotoPageModel)((FrameworkElement)sender).DataContext;
            if (moving != droppedOn) {
                // Record snapshot before reordering pages
                undoRedoManager.RecordSnapshot();
                book.Pages.Remove(moving!);
                int newIndex = book.Pages.IndexOf(droppedOn);
                if (e.GetPosition(droppedOnElt).Y > (droppedOnElt.ActualHeight / 2))
                    newIndex++;
                book.Pages.Insert(newIndex, moving!);
                tableOfContentsListbox.SelectedItem = moving;
            }
            e.Handled = true;
        }
    }

    private void listboxitem_PreviewMouseMove(object sender, MouseEventArgs e) {
        if (Mouse.LeftButton == MouseButtonState.Pressed && !twoPageMode) {
            var itemElt = (FrameworkElement)sender;
            PhotoPageModel page = (PhotoPageModel)((FrameworkElement)sender).DataContext;
            DataObject d = new DataObject(page);
            DragDrop.DoDragDrop(itemElt, d, DragDropEffects.Move);
        }
    }

    void IScreen.Activate(ImageOrigin? focus) {
        //RootControl.Instance.loader.Mode = LoaderMode.PageDesigner;
    }

    void IScreen.Deactivate() {

    }

    //private void TemplateChooserGrid_MouseMove(object sender, MouseEventArgs e) {
    //    Grid g = (Grid)sender;
    //    if (g.ToolTip == null) {
    //        var model = (PhotoPageModel)g.DataContext;

    //        var tt = new ToolTip();
    //        g.ToolTip = tt;
    //        tt.Content = model.TemplateName; // won't change over lifetime
    //        ToolTipService.SetShowDuration(tt, 999999);
    //    }
    //    //  ToolTip="{Binding TemplateName}"
    //}
}
