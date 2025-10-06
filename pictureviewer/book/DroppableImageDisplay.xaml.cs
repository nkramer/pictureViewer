using Pictureviewer.Core;
using Pictureviewer.Library;
using Pictureviewer.Shell;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pictureviewer.Book {
    /// <summary>
    /// Interaction logic for DroppableImageDisplay.xaml
    /// </summary>
    public partial class DroppableImageDisplay : ImageDisplay {
        private RootControl root = RootControl.Instance;
        private int imageIndex; // Index into PhotoPageModel.Images
        private PhotoPageModel model;
        private Path BigX = null;

        public DroppableImageDisplay() {
            InitializeBigX();

            this.ContextMenuOpening += new ContextMenuEventHandler(DroppableImageDisplay_ContextMenuOpening);
            this.Drop += new DragEventHandler(display_Drop);
            this.AllowDrop = true;
            this.Loaded += new RoutedEventHandler(DroppableImageDisplay_Loaded);
            this.Unloaded += new RoutedEventHandler(DroppableImageDisplay_Unloaded);
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(DroppableImageDisplay_DataContextChanged);
            this.MouseLeftButtonDown += new MouseButtonEventHandler(DroppableImageDisplay_MouseLeftButtonDown);
            this.MouseEnter += new MouseEventHandler(DroppableImageDisplay_MouseEnter);
        }

        private void InitializeBigX() {
            var xform = new ScaleTransform();
            var b = new Binding("Flipped");
            b.Converter = (IValueConverter)FindResource("BoolToScaleFlipConverter");
            //xform.SetBinding(ScaleTransform.ScaleXProperty, b);
            BindingOperations.SetBinding(xform, ScaleTransform.ScaleXProperty, b);
            this.RenderTransform = xform;
            //(this.RenderTransform as TransformGroup).Children.Add(xform);

            BigX = new Path();
            this.Children.Add(BigX);
            BigX.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0xD5, 0xD5, 0xD5));
            BigX.Stretch = Stretch.Fill;
            BigX.StrokeMiterLimit = 0;
            BigX.StrokeThickness = 3;
            BigX.Data = Geometry.Parse("M0,0 L30,30 M0,30 L30,0 L30,30 L0,30 L0,0 L30,0 z");
        }

        // delay create for perf
        void DroppableImageDisplay_MouseEnter(object sender, MouseEventArgs e) {
            if (this.ToolTip == null) {
                var tt = new ToolTip();
                this.ToolTip = tt;
                tt.SetBinding(System.Windows.Controls.ToolTip.ContentProperty,
                    "ImageDisplay.ImageInfo.ImageMetadataText");
                ToolTipService.SetShowDuration(tt, 999999);
            }
        }

        // delay create for perf
        void DroppableImageDisplay_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
            if (this.ContextMenu == null) {
                var menu = new ContextMenu();
                this.ContextMenu = menu;
                var item = new MenuItem();
                menu.Items.Add(item);
                item.Header = "Remove photo";
                item.Click += MenuItem_Remove;

                // cancel showing of a different ContextMenu and show the right one
                // see http://msdn.microsoft.com/en-us/library/bb613568.aspx
                e.Handled = true;
                menu.PlacementTarget = this;
                menu.IsOpen = true;
            }
        }

        private PhotoPageModel Model {
            get { return this.DataContext as PhotoPageModel; }
        }

        //// Property so you can set it in xaml
        //public PhotoPageModel Model
        //{
        //    get { return model; }
        //    set { model = value; }
        //}

        // Property so you can set it in xaml
        public int ImageIndex {
            get { return imageIndex; }
            set { imageIndex = value; }
        }

        private ImageOrigin Origin {
            get {
                if (model != null && imageIndex < model.Images.Count) {
                    ImageOrigin origin = model.Images[this.imageIndex];
                    return origin;
                }
                return null;
            }
        }

        // Replace all usages of PhotoGrid.PhotoDragData with PhotoDragData
        // Assuming PhotoDragData is a top-level class, not nested in PhotoGrid

        void DroppableImageDisplay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (this.ImageDisplay.ImageOrigin != null) {
                var data = new PhotoDragData() {
                    ImageOrigin = this.ImageDisplay.ImageOrigin,
                    SwapWithOrigin = true,
                };
                DataObject dragData = new DataObject(data);
                DragDropEffects res = DragDrop.DoDragDrop(this, dragData, DragDropEffects.Copy);
                //if (res != DragDropEffects.None) {
                //    BeginSetImage(null);
                //    // TODO: seems odd we set ImageInfo rather than ImageOrigin, but thats the api
                //    //this.ImageDisplay.ImageInfo = null;
                //    //this.ImageDisplay.ImageOrigin = null;
                //}
            }
        }

        void DroppableImageDisplay_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            ModelChanged();
        }

        void DroppableImageDisplay_Loaded(object sender, RoutedEventArgs e) {
            ModelChanged();
        }

        private void ModelChanged() {
            PhotoPageModel oldModel = this.model;
            this.model = this.Model;
            //new Binding("Images[" + this.imageIndex + "]");

            if (oldModel != null) {
                oldModel.Images.CollectionChanged -= new NotifyCollectionChangedEventHandler(Images_CollectionChanged);
                oldModel = null;
            }

            if (model != null) {// !Design time
                model.Images.CollectionChanged += new NotifyCollectionChangedEventHandler(Images_CollectionChanged);
            }
            BeginSetImage(Origin);
        }

        void DroppableImageDisplay_Unloaded(object sender, RoutedEventArgs e) {
            this.DataContext = null;
            if (this.model != null) {
                model.Images.CollectionChanged -= new NotifyCollectionChangedEventHandler(Images_CollectionChanged);
                this.model = null;
            }
            ModelChanged(); // unhook CollectionChanged
        }

        void Images_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (model != null && imageIndex < model.Images.Count) {
                if (e.NewStartingIndex == this.ImageIndex) {
                    ImageOrigin origin = model.Images[this.imageIndex];
                    BeginSetImage(origin);
                }
            }
        }

        private PhotoPageView GetPageView() {
            Visual e = this;
            for (; ; ) {
                if (e == null) return null;
                //Debug.Assert(e != null); // DroppableImageDisplay should always be inside a PhotoPageView
                if (e is PhotoPageView)
                    return (PhotoPageView)e;
                var vparent = VisualTreeHelper.GetParent(e);
                if (vparent != null) {
                    e = vparent as Visual;
                } else if (e is FrameworkElement) {
                    FrameworkElement fe = (FrameworkElement)e;
                    e = (FrameworkElement)fe.Parent;
                }
            }
        }

        private void BeginSetImage(ImageOrigin origin) {
            this.ImageInfo = null;
            this.ImageOrigin = origin;

            if (origin == null) {
                BigX.Visibility = System.Windows.Visibility.Visible;
            } else {
                double clientwidth;
                double clientheight;
                ImageDisplay.GetSizeInPixels(this, out clientwidth, out clientheight);
                //Debug.WriteLine("" + clientwidth + " " + clientheight);

                var v = GetPageView();
                if (GetPageView() != null && GetPageView().IsPrintMode) {
                    // width/height are ignored for scalingBehavior.Print
                    var im = RootControl.Instance.loader.LoadSync(
                        new LoadRequest(origin, (int)clientwidth, (int)clientheight, ScalingBehavior.Print));
                    this.ImageInfo = im;
                    BigX.Visibility = Visibility.Collapsed;
                } else {
                    int width = (int)clientwidth;
                    int height = (int)clientheight;
                    //if (this.ActualHeight > 0 && this.ActualWidth > 0) 
                    if (width > 0 && height > 0) {
                        // undone: display is not pixel aligned.  hack -- use bigger bitmap
                        if (width > 150 && height > 150) {
                            width *= 2;
                            height *= 2;
                        }
                        RootControl.Instance.loader.BeginLoad(new LoadRequest(origin,
                            width, height, ScalingBehavior.Small),
                            (info) => {
                                if (info.Origin == origin) {
                                    // guard against callbacks out of order
                                    this.ImageInfo = info;
                                    BigX.Visibility = Visibility.Collapsed;
                                }
                            });
                        this.ImageDisplay.ResetRotation(origin); // needed?
                    }
                }
            }
        }

        void display_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(typeof(PhotoDragData))) {
                var data = e.Data.GetData(typeof(PhotoDragData)) as PhotoDragData;
                ImageOrigin origin = data.ImageOrigin;

                int otherIndex = -1;
                ImageOrigin oldImage = null;

                if (data.SwapWithOrigin) {
                    otherIndex = model.Images.IndexOf(data.ImageOrigin);
                    if (this.imageIndex < model.Images.Count) {
                        oldImage = model.Images[this.imageIndex];
                    }
                }

                int i = model.Images.Count - 1;
                while (i < this.imageIndex) {
                    model.Images.Add(null);
                    i++;
                }
                model.Images[this.imageIndex] = origin;

                if (data.SwapWithOrigin) {
                    model.Images[otherIndex] = oldImage;
                }
            }

        }

        public ImageDisplay ImageDisplay { get { return this; } }

        private void MenuItem_Remove(object sender, RoutedEventArgs e) {
            if (this.Origin != null) {
                var page = this.Model;
                page.Images[imageIndex] = null;
            }
        }
    }
}
