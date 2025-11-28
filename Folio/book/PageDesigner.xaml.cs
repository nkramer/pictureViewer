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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace Folio.Book {
    // Represents a book entry in the book selector dropdown
    public class BookInfo {
        public string DisplayName { get; set; }
        public string FilePath { get; set; }
        public bool IsNewBook { get; set; }

        public override string ToString() {
            return DisplayName;
        }
    }

    public partial class PageDesigner : UserControl, INotifyPropertyChanged, IScreen {
        private CommandHelper commands;
        private BookModel book = null;// = new BookModel();
        private bool twoPageMode = false;
        private bool isLoadingBook = false; // Flag to prevent recursive loading

        // HACK: seems easier to implement INotifyPropertyChanged than make everything a dependency property
        public event PropertyChangedEventHandler PropertyChanged;

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

            if (RootControl.Instance.book == null) {
                // First time opening PageDesigner - load default book
                RootControl.Instance.currentBookPath = RootControl.dbDir + @"\testPhotoBook.xml";
                RootControl.Instance.book = BookModel.Parse(RootControl.Instance.currentBookPath);
                RootControl.Instance.book.SelectedPage = RootControl.Instance.book.Pages[0];
            } else if (RootControl.Instance.currentBookPath == null) {
                Debug.Fail("WTF? A book with no path?");
                // Book exists but path wasn't tracked - default to testPhotoBook.xml
                RootControl.Instance.currentBookPath = RootControl.dbDir + @"\testPhotoBook.xml";
            }

            book = RootControl.Instance.book;
            book.PropertyChanged += new PropertyChangedEventHandler(book_PropertyChanged); // BUG?: never unhooked
            book.ImagesChanged += new EventHandler(book_ImagesChanged); // BUG?: never unhooked

            this.DataContext = book;
            SetTwoPageMode(false);

            this.commands = new CommandHelper(this);
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
            bookList.Add(new BookInfo {
                DisplayName = "New Book...",
                FilePath = null,
                IsNewBook = true
            });

            // Get all .xml files from dbDir
            if (Directory.Exists(RootControl.dbDir)) {
                var xmlFiles = Directory.GetFiles(RootControl.dbDir, "*.xml");
                foreach (var filePath in xmlFiles.OrderBy(f => Path.GetFileNameWithoutExtension(f))) {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    bookList.Add(new BookInfo {
                        DisplayName = fileName,
                        FilePath = filePath,
                        IsNewBook = false
                    });
                }
            }

            bookSelector.ItemsSource = bookList;

            // Select the current book if it exists
            if (RootControl.Instance.book != null && RootControl.Instance.currentBookPath != null) {
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
                LoadBook(selectedBook.FilePath);
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
                    var doc = new XDocument(new XElement("PhotoBook",
                        newBook.Pages.Select(m => m.Persist())));
                    doc.Save(filePath);

                    // Load the new book
                    LoadBook(filePath);

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

        private void LoadBook(string filePath) {
            if (filePath == RootControl.Instance.currentBookPath) 
                return; // Already loaded, if we reload it'll stomp any changes made in memory 

            if (!File.Exists(filePath)) {
                ThemedMessageBox.Show($"Book file not found: {filePath}");
                return;
            }

            try {
                // Load the new book
                var newBook = BookModel.Parse(filePath);
                isLoadingBook = true;

                // Unhook events from old book if needed
                if (book != null) {
                    book.PropertyChanged -= book_PropertyChanged;
                    book.ImagesChanged -= book_ImagesChanged;
                }

                // Check if we're reloading the same book to preserve the selected page
                bool isSameBook = (filePath == RootControl.Instance.currentBookPath);
                int preservedPageIndex = -1;

                if (isSameBook && book != null && book.SelectedPage != null) {
                    preservedPageIndex = book.Pages.IndexOf(book.SelectedPage);
                }

                // Update the book reference
                this.book = newBook;
                RootControl.Instance.book = newBook;
                RootControl.Instance.currentBookPath = filePath;

                // Hook up events to new book
                book.PropertyChanged += book_PropertyChanged;
                book.ImagesChanged += book_ImagesChanged;

                // Update the data context
                this.DataContext = book;

                // Update the table of contents
                SetTwoPageMode(twoPageMode);
                tableOfContentsListbox.SelectedItem = book.SelectedPage;

                if (newBook.Pages.Count > 0) {
                    // If reloading the same book, restore the selected page
                    if (isSameBook && preservedPageIndex >= 0 && preservedPageIndex < newBook.Pages.Count) {
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
            } catch (Exception ex) {
                ThemedMessageBox.Show($"Error loading book: {ex.Message}");
            } finally {
                isLoadingBook = false;
            }
        }

        private void RevertBookSelection() {
            // Revert to the currently loaded book
            if (book != null && RootControl.Instance.currentBookPath != null) {
                var currentBook = ((List<BookInfo>)bookSelector.ItemsSource)
                    .FirstOrDefault(b => b.FilePath == RootControl.Instance.currentBookPath);
                if (currentBook != null) {
                    bookSelector.SelectedItem = currentBook;
                }
            }
        }

        void book_ImagesChanged(object sender, EventArgs e) {
            RootControl.Instance.UpdateCache();
        }

        void book_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            // todo: handle case where RootControl.book changes -- unhook listener, etc.
            if (e.PropertyName == "" || e.PropertyName == "SelectedPage") {
                RootControl.Instance.UpdateCache();
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
            RootControl.Instance.loader.SetTargetSize((int)pageholder.ActualWidth * 2, (int)pageholder.ActualHeight * 2);
            RootControl.Instance.loader.PrefetchPolicy = PrefetchPolicy.PageDesigner;
            return result;
        }


        void PageDesigner_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            ScrollViewer sv = GetTOCScrollViewer();
            int firstVisibleItem = (int)sv.VerticalOffset;
        }

        private void CreateCommands() {
            Command command;

            command = new Command();
            command.Key = Key.W;
            command.Text = "Save database (write)";
            command.Execute += delegate () {
                var doc = new XDocument(new XElement("PhotoBook",
                    book.Pages.Select(m => m.Persist())));
                doc.Save(RootControl.Instance.currentBookPath);
                doc.Save(RootControl.dbDirCopy + @"\" + Path.GetFileName(RootControl.Instance.currentBookPath));
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
            command.Key = Key.F;
            command.Text = "Flip";
            command.Execute += delegate () {
                if (SelectedPage != null)
                    SelectedPage.Flipped = !SelectedPage.Flipped;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.B;
            command.Text = "Background";
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
                RootControl.Instance.PushScreen(new BookViewerFullscreen());
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
            command.Execute += delegate () {
                var page = new PhotoPageModel(book);
                book.Pages.Insert(tableOfContentsListbox.SelectedIndex + 1, page);
                tableOfContentsListbox.SelectedItem = page;
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
            command.Execute += delegate () {
                int i = tableOfContentsListbox.SelectedIndex;
                book.Pages.Remove(tableOfContentsListbox.SelectedItem as PhotoPageModel);
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
            var dialog = new TemplateChooserDialog(this.SelectedPage, book);
            bool? result = dialog.ShowDialog();

            if (result == true && dialog.SelectedTemplateName != null) {
                SelectedPage.TemplateName = dialog.SelectedTemplateName;
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
                    SelectedPage = t.Right;
            } else {
                SelectedPage = null;
            }
        }

        private PhotoPageModel SelectedPage {
            get {
                return book.SelectedPage;
            }
            set {
                book.SelectedPage = value;
            }
        }


        private void listboxitem_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(typeof(PhotoPageModel))) {
                PhotoPageModel moving = e.Data.GetData(typeof(PhotoPageModel)) as PhotoPageModel;
                FrameworkElement droppedOnElt = (FrameworkElement)sender;
                PhotoPageModel droppedOn = (PhotoPageModel)((FrameworkElement)sender).DataContext;
                if (moving != droppedOn) {
                    book.Pages.Remove(moving);
                    int newIndex = book.Pages.IndexOf(droppedOn);
                    if (e.GetPosition(droppedOnElt).Y > (droppedOnElt.ActualHeight / 2))
                        newIndex++;
                    book.Pages.Insert(newIndex, moving);
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

        void IScreen.Activate(ImageOrigin focus) {
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
}
