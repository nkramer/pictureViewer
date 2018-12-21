using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using System.Diagnostics;
using System.Data;
using System.ComponentModel;
using System.IO;
using System.Windows.Interop;
using System.Windows.Threading;

namespace pictureviewer {
    public partial class PageDesigner : UserControl, INotifyPropertyChanged, IScreen {
        private CommandHelper commands;
        private BookModel book=null;// = new BookModel();
        private bool twoPageMode = false;

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
                RootControl.Instance.book = new BookModel();
                RootControl.Instance.book.Parse(); // init this.models
                RootControl.Instance.book.SelectedPage = RootControl.Instance.book.Pages[0];
            }
            book = RootControl.Instance.book;
            book.PropertyChanged += new PropertyChangedEventHandler(book_PropertyChanged);
            book.ImagesChanged += new EventHandler(book_ImagesChanged);

            this.DataContext = book;
            SetTwoPageMode(false);

            this.commands = new CommandHelper(this);
            CreateCommands();

            var p = new PhotoGrid(RootControl.Instance);
            p.MaxPhotosToDisplay = 80; // hack, should calculate by window size
            p.Background = Brushes.Transparent;
            p.Mode = PhotoGridMode.Designer;
            b.Child = p;
            p.filters.Visibility = Visibility.Collapsed;

            tableOfContentsListbox.SelectedItem = book.SelectedPage;

            this.Loaded += new RoutedEventHandler(PageDesigner_Loaded);
            this.Focusable = true;
            this.KeyDown += new KeyEventHandler(PageDesigner_KeyDown);
            this.LostFocus += new RoutedEventHandler(PageDesigner_LostFocus);
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
            int firstVisibleItem = (int) sv.VerticalOffset;
            // returns item # not pixel count in our case
        }

        protected void NotifyPropertyChanged(String info) {
            if (PropertyChanged != null) {
                var args = new PropertyChangedEventArgs(info);
                PropertyChanged(this, args);
            }
        }

        void PageDesigner_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is RichTextBox && e.Key == Key.Escape) {
                this.Focus();
            } else if (templates.Visibility == System.Windows.Visibility.Visible && e.Key == Key.Escape) {
                SetTemplateAndHideListbox(null);
                e.Handled = true;
            }
        }

        private void TemplateName(DataTemplate t) {
        }

        void PageDesigner_Loaded(object sender, RoutedEventArgs e)
        {
            bool res = this.Focus();
            this.GetTOCScrollViewer().ScrollChanged += new ScrollChangedEventHandler(PageDesigner_ScrollChanged);
            RootControl.Instance.loader.SetTargetSize((int)pageholder.ActualWidth*2, (int)pageholder.ActualHeight*2);
            RootControl.Instance.loader.Mode = LoaderMode.PageDesigner;
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
            command.Execute += delegate() {
                var doc = new XDocument(new XElement("PhotoBook",
                    book.Pages.Select(m => m.Persist())));
                doc.Save(RootControl.dbDir + @"\testPhotoBook.xml");
                doc.Save(RootControl.dbDirCopy + @"\testPhotoBook.xml");
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.P;
            command.ModifierKeys = ModifierKeys.Shift;
            command.Text = "Print";
            command.Execute += delegate() {
                PrintBook();
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.F;
            command.Text = "Flip";
            command.Execute += delegate() {
                SelectedPage.Flipped = !SelectedPage.Flipped; 
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.B;
            command.Text = "Background";
            command.Execute += delegate() {
                var fg = SelectedPage.ForegroundColor;
                var bg = SelectedPage.BackgroundColor;
                SelectedPage.BackgroundColor = fg;
                SelectedPage.ForegroundColor = bg;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.F11;
            command.Text = "Fullscreen";
            command.Execute += delegate() {
                RootControl.Instance.PushScreen(new BookViewerFullscreen());
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.T;
            command.Text = "Choose template";
            command.Execute += delegate() {
                ShowTemplateChooser();
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.M;
            command.Text = "new page";
            command.Execute += delegate() {
                var page = new PhotoPageModel(book);
                book.Pages.Insert(tableOfContentsListbox.SelectedIndex + 1, page);
                tableOfContentsListbox.SelectedItem = page;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.D;
            command.Text = "double pageview";
            command.Execute += delegate() {
                SetTwoPageMode(!twoPageMode);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.Delete;
            command.Text = "Delete page";
            command.Execute += delegate() {
                int i = tableOfContentsListbox.SelectedIndex;
                book.Pages.Remove(tableOfContentsListbox.SelectedItem as PhotoPageModel);
                tableOfContentsListbox.SelectedIndex = Math.Min(i, book.Pages.Count - 1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Next page";
            command.Key = Key.Right;
            command.Execute += delegate() {
                NextPage(1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Previous page";
            command.Key = Key.Left;
            command.Execute += delegate() {
                NextPage(-1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Next page";
            command.Key = Key.Down;
            command.HasMenuItem = false;
            command.Execute += delegate() {
                NextPage(1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Previous page";
            command.Key = Key.Up;
            command.HasMenuItem = false;
            command.Execute += delegate() {
                NextPage(-1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Forward 1 page";
            command.Key = Key.PageDown;
            command.DisplayKey = "PageDown";
            command.Execute += delegate() {
                NextPage(1);
            };
            commands.AddCommand(command);
#if WPF
            commands.AddBinding(command, MediaCommands.FastForward);
            commands.AddBinding(command, MediaCommands.NextTrack);
#endif

            command = new Command();
            command.Text = "Backward 1 page";
            command.Key = Key.PageUp;
            command.Execute += delegate() {
                NextPage(-1);
            };
            commands.AddCommand(command);
#if WPF
            commands.AddBinding(command, MediaCommands.Rewind);
            commands.AddBinding(command, MediaCommands.PreviousTrack);
#endif
        }

        public void ShowTemplateChooser() {
            // temporary code migration tool
            //PhotoPageView.GetAllTemplateNames().Select(name => {
            //    var t = PhotoPageView.GetTemplate(name);
            //    var dobj = t.LoadContent();
            //    Debug.Write("            \"");
            //    Debug.Write(name + " ");
            //    AspectPreservingGrid.DebugPrintTemplateShortString(dobj as Grid);
            //    Debug.WriteLine("\",");
            //    return "";
            //}).ToArray();            
            
            templates.Visibility = System.Windows.Visibility.Visible;
            var page = this.SelectedPage;// (PhotoPageModel)tableOfContentsListbox.SelectedItem; //this.book.SelectedPage;
            List<PhotoPageModel> samplePages = PhotoPageView.GetAllTemplateNames().Select(name => {
                var t = PhotoPageView.GetTemplate(name);
                var sample = page.Clone();
                sample.TemplateName = name;
                return sample;
            }).ToList();
            templates.ItemsSource = samplePages;
        }

        private void PrintBook() {
            int pagenum = 0;
            foreach (PhotoPageModel page in book.Pages) {
                DoWithOOMTryCatch(() => PrintPage(page, pagenum));
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

        private void PrintPage(PhotoPageModel page, int pagenum) {
            //double scaleFactor = 1; // todo: = 3
            double scaleFactor = 3;
            Size size = new Size(1125 * scaleFactor, 875 * scaleFactor);
            var target = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Default);

            var grid = new Grid { Width = size.Width, Height = size.Height };
            var p = new PhotoPageView();
            p.IsPrintMode = true;
            p.Page = page;
            p.DataContext = this.DataContext;
            grid.Children.Add(p);

            // to get Loaded event & databinding, need a PresentationSource
            using (var source = new HwndSource(new HwndSourceParameters()) { RootVisual = grid }) {
                grid.Measure(size);
                grid.Arrange(new Rect(size));
                grid.UpdateLayout();

                // run databinding.  Also clear out any items RichTextBox queues up, if you don't
                // you'll eventually hit OutOfMemoryException.
                //Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(() => { }));
                Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

                target.Render(grid);
            }

            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 100; // max
            encoder.Frames.Add(BitmapFrame.Create(target));
            string filename = String.Format(RootControl.dbDir+ @"\output\page-{0:D2}.jpg", pagenum);
            using (Stream s = File.Create(filename)) {
                encoder.Save(s);
            }
        }

        private void NextPage(int increment) {
            tableOfContentsListbox.SelectedIndex = Math.Min(tableOfContentsListbox.SelectedIndex + increment, book.Pages.Count - 1);
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

        private void templates_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (templates.SelectedItem != null) {
                string templateName = ((PhotoPageModel)templates.SelectedItem).TemplateName;
                SetTemplateAndHideListbox(templateName);
            }
        }

        public void SetTemplateAndHideListbox(string templateName) {
            if (templateName != null) {
                SelectedPage.TemplateName = templateName;
            }
            templates.Visibility = Visibility.Collapsed;
            this.Focus();
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
            if (Mouse.LeftButton == MouseButtonState.Pressed) {
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
