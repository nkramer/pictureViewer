using Folio.Core;
using Folio.Library;
using Folio.Shell;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Runtime.InteropServices;

namespace Folio.Book {
    /// <summary>
    /// Interaction logic for DroppableImageDisplay.xaml
    /// </summary>
    public partial class DroppableImageDisplay : ImageDisplay {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private RootControl root = RootControl.Instance;
        private int imageIndex; // Index into PhotoPageModel.Images
        private PhotoPageModel page;
        private Path BigX = null;
        private Popup dragFeedbackPopup = null;
        private Image dragFeedbackImage = null;

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
            this.GiveFeedback += new GiveFeedbackEventHandler(DroppableImageDisplay_GiveFeedback);
            this.QueryContinueDrag += new QueryContinueDragEventHandler(DroppableImageDisplay_QueryContinueDrag);
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
            BigX.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x88, 0x88, 0x88));
            BigX.Stretch = Stretch.Fill;
            BigX.StrokeMiterLimit = 0;
            BigX.StrokeThickness = 12;
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
                if (page != null && imageIndex < page.Images.Count) {
                    ImageOrigin origin = page.Images[this.imageIndex];
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
                    SourcePage = this.page,
                    SourceIndex = this.imageIndex
                };
                DataObject dragData = new DataObject(data);

                // Create drag feedback before starting drag
                CreateDragFeedback();

                DragDropEffects res = DragDrop.DoDragDrop(this, dragData, DragDropEffects.Copy);

                // Clean up drag feedback after drag completes
                HideDragFeedback();

                //if (res != DragDropEffects.None) {
                //    BeginSetImage(null);
                //    // TODO: seems odd we set ImageInfo rather than ImageOrigin, but thats the api
                //    //this.ImageDisplay.ImageInfo = null;
                //    //this.ImageDisplay.ImageOrigin = null;
                //}
            }
        }

        void DroppableImageDisplay_GiveFeedback(object sender, GiveFeedbackEventArgs e) {
            // Use custom cursor feedback
            e.UseDefaultCursors = false;
            e.Handled = true;

            // Update popup position to follow cursor
            UpdateDragFeedbackPosition();
        }

        void DroppableImageDisplay_QueryContinueDrag(object sender, QueryContinueDragEventArgs e) {
            // Update popup position during drag
            UpdateDragFeedbackPosition();
        }

        private void CreateDragFeedback() {
            if (this.ImageDisplay.ImageInfo != null && this.ImageDisplay.ImageInfo.scaledSource != null) {
                // Create the image for drag feedback
                dragFeedbackImage = new Image();
                dragFeedbackImage.Source = this.ImageDisplay.ImageInfo.scaledSource;
                dragFeedbackImage.Width = 100;
                dragFeedbackImage.Height = 100;
                dragFeedbackImage.Opacity = 0.6; // Translucent
                dragFeedbackImage.Stretch = Stretch.Uniform;

                // Create a border around the image
                var border = new Border();
                border.Child = dragFeedbackImage;
                border.BorderBrush = new SolidColorBrush(Colors.Gray);
                border.BorderThickness = new Thickness(2);
                border.Background = new SolidColorBrush(Colors.White);
                border.Opacity = 0.8;

                // Create popup with absolute positioning
                dragFeedbackPopup = new Popup();
                dragFeedbackPopup.Child = border;
                dragFeedbackPopup.AllowsTransparency = true;
                dragFeedbackPopup.IsHitTestVisible = false;
                dragFeedbackPopup.Placement = PlacementMode.Absolute;

                // Position and show popup
                UpdateDragFeedbackPosition();
                dragFeedbackPopup.IsOpen = true;
            }
        }

        private void UpdateDragFeedbackPosition() {
            if (dragFeedbackPopup != null && dragFeedbackPopup.IsOpen) {
                // Get cursor position in screen coordinates (physical pixels)
                POINT cursorPos;
                if (GetCursorPos(out cursorPos)) {
                    // Convert screen coordinates to device-independent pixels (DIPs)
                    // WPF Popup uses DIPs, not physical pixels
                    var window = Window.GetWindow(this);
                    if (window != null) {
                        var source = PresentationSource.FromVisual(window);
                        if (source != null) {
                            double dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                            double dpiScaleY = source.CompositionTarget.TransformToDevice.M22;

                            // Convert to DIPs and add offset
                            dragFeedbackPopup.HorizontalOffset = (cursorPos.X / dpiScaleX) + 10;
                            dragFeedbackPopup.VerticalOffset = (cursorPos.Y / dpiScaleY) + 10;
                            return;
                        }
                    }

                    // Fallback if we can't get DPI scaling (assume 96 DPI = 1.0 scale)
                    dragFeedbackPopup.HorizontalOffset = cursorPos.X + 10;
                    dragFeedbackPopup.VerticalOffset = cursorPos.Y + 10;
                }
            }
        }

        private void HideDragFeedback() {
            if (dragFeedbackPopup != null) {
                dragFeedbackPopup.IsOpen = false;
                dragFeedbackPopup = null;
            }
            dragFeedbackImage = null;
        }

        void DroppableImageDisplay_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            ModelChanged();
        }

        void DroppableImageDisplay_Loaded(object sender, RoutedEventArgs e) {
            ModelChanged();
        }

        private void ModelChanged() {
            PhotoPageModel oldModel = this.page;
            this.page = this.Model;
            //new Binding("Images[" + this.imageIndex + "]");

            if (oldModel != null) {
                oldModel.Images.CollectionChanged -= new NotifyCollectionChangedEventHandler(Images_CollectionChanged);
                oldModel = null;
            }

            if (page != null) {// !Design time
                page.Images.CollectionChanged += new NotifyCollectionChangedEventHandler(Images_CollectionChanged);
            }
            BeginSetImage(Origin);
        }

        void DroppableImageDisplay_Unloaded(object sender, RoutedEventArgs e) {
            this.DataContext = null;
            if (this.page != null) {
                page.Images.CollectionChanged -= new NotifyCollectionChangedEventHandler(Images_CollectionChanged);
                this.page = null;
            }
            ModelChanged(); // unhook CollectionChanged
        }

        void Images_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (page != null && imageIndex < page.Images.Count) {
                if (e.NewStartingIndex == this.ImageIndex) {
                    ImageOrigin origin = page.Images[this.imageIndex];
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
                BigX.Visibility = Visibility.Visible;
                // put DesiredAspectRatio back to template default
                AspectPreservingGrid.SetDesiredAspectRatio(this, AspectPreservingGrid.GetDesiredAspectRatio(this));
            } else {
                double clientwidth;
                double clientheight;
                ImageDisplay.GetSizeInPhysicalPixels(this, out clientwidth, out clientheight);
                //Debug.WriteLine("" + clientwidth + " " + clientheight);

                var v = GetPageView();
                if (GetPageView() != null && GetPageView().IsPrintMode) {
                    // width/height are ignored for scalingBehavior.Print
                    ImageInfo im = RootControl.Instance.loader.LoadSync(
                        new LoadRequest(origin, (int)clientwidth, (int)clientheight, ScalingBehavior.Print));
                    this.ImageInfo = im;
                    UpdateAspectRatioFromImage(im);
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
                                    UpdateAspectRatioFromImage(info);
                                    BigX.Visibility = Visibility.Collapsed;
                                }
                            });
                        this.ImageDisplay.ResetRotation(origin); // needed?
                    }
                }
            }
        }

        // Updates the aspect ratio based on the loaded image's actual pixel dimensions.
        // This allows images to use their native aspect ratio instead of the template's default.
        private void UpdateAspectRatioFromImage(ImageInfo info) {
            if (info != null && info.IsValid && info.PixelWidth > 0 && info.PixelHeight > 0) {
                // Create aspect ratio from image pixel dimensions and set as desired
                Ratio aspectRatio = new Ratio(info.PixelWidth, info.PixelHeight);
                if (info.RotationDisplayAdjustment == 90 || info.RotationDisplayAdjustment == 270 || info.RotationDisplayAdjustment == -90)
                    aspectRatio = new Ratio(info.PixelHeight, info.PixelWidth);
                AspectPreservingGrid.SetDesiredAspectRatio(this, aspectRatio);
            }
        }

        void display_Drop(object sender, DragEventArgs e) {
            var target = this;
            if (e.Data.GetDataPresent(typeof(PhotoDragData))) {
                var drag = e.Data.GetData(typeof(PhotoDragData)) as PhotoDragData;
                ImageOrigin source = drag.ImageOrigin;
                ImageOrigin oldTargetImage = null; 

                if (drag.SwapWithOrigin && target.imageIndex < target.page.Images.Count) {
                    oldTargetImage = target.page.Images[target.imageIndex];
                }

                // Expand target page collection if needed
                int i = target.page.Images.Count - 1;
                while (i < target.imageIndex) {
                    target.page.Images.Add(null);
                    i++;
                }

                // Set the image at the drop target
                target.page.Images[target.imageIndex] = source;

                if (drag.SwapWithOrigin) {
                    //Debug.Assert(oldTargetImage != null);
                    Debug.Assert(drag.SourcePage != null);
                    Debug.Assert(drag.SourceIndex >= 0 && drag.SourceIndex < drag.SourcePage.Images.Count);
                    drag.SourcePage.Images[drag.SourceIndex] = oldTargetImage;
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
