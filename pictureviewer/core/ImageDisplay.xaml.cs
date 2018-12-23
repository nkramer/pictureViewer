using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.ComponentModel;

namespace pictureviewer {
    // basically a Image element that snaps to pixels and supports zoom & 
    // rotation with clever animations between those states
    //
    // For the Silverlight version, I considered using a MultiScaleImage (DeepZoom),
    // but wasn't satisfied with the image quality when it's scaled to fit the screen.  
    // (Image is too soft -- acceptable for most applications, but not when
    // one of your tasks is to decide if the photo was in focus)
    // DeepZoom worked pretty good for the zoomed in image, and would have superior 
    // download times to what we are currently using, although it's kind of 
    // awkward to integrate into the app.
    public partial class ImageDisplay : Canvas, INotifyPropertyChanged {
        private bool zoom;
        private bool grayscaleMode;
        private double rotation;
        private bool flip;
        private ImageInfo imageInfo;

        private double zoomOffsetX = 0;
        private double zoomOffsetY = 0;
        private double lastMouseX;
        private double lastMouseY;

        // 0 = no crop marks
        private double cropMarkAspectRatio;

        private bool isMouseCaptured = false;
        private GrayscaleEffect effect = null;
        
        private Image imageElementOld = null;
        private Rectangle cropMark = null;

        private Image imageElement;
        private ScaleTransform scaleTransform;
        private RotateTransform rotateTransform;
        private TranslateTransform translatePanningTransform;
        private ScaleTransform flipTransform;

        public ImageDisplay() {
            InitializeComponent();
            this.MouseLeftButtonDown += new MouseButtonEventHandler(ImageDisplay_MouseLeftButtonDown);
            this.MouseLeftButtonUp += new MouseButtonEventHandler(ImageDisplay_MouseLeftButtonUp);
            this.MouseMove += new MouseEventHandler(ImageDisplay_MouseMove);

#if WPF
            // why is this needed?  I set imageElement.Height to a whole #, but imageElement.ActualHeight comes back fractional!
            this.SnapsToDevicePixels = true;
#endif
            imageElement.ImageFailed += new EventHandler<ExceptionRoutedEventArgs>(imageElement_ImageFailed);
        }

        //    <Canvas x:Class="pictureviewer.ImageDisplay"
        //xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        //xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        //Background="Transparent" 
        //RenderTransformOrigin="0.5,0.5" HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
        //>

        //<Image Name="imageElement" RenderTransformOrigin="0.5,0.5" >
        //    <UIElement.RenderTransform>
        //        <TransformGroup>
        //            <ScaleTransform x:Name="scaleTransform" ScaleX="1" ScaleY="1"/>
        //            <RotateTransform x:Name="rotateTransform" Angle="0"/>
        //            <TranslateTransform x:Name="translatePanningTransform" X="0" Y="0"/>
        //            <ScaleTransform x:Name="flipTransform" ScaleX="1" ScaleY="1"/>
        //        </TransformGroup>
        //    </UIElement.RenderTransform>
        //</Image>

        private void InitializeComponent() {
            this.Background = Brushes.Transparent;
            this.RenderTransformOrigin = new Point(.5, .5);
            this.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            this.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            imageElement = new Image();
            this.Children.Add(imageElement);
            var t = new TransformGroup();
            scaleTransform = new ScaleTransform(1, 1);
            t.Children.Add(scaleTransform);
            rotateTransform = new RotateTransform(0);
            t.Children.Add(rotateTransform);
            translatePanningTransform = new TranslateTransform(0, 0);
            t.Children.Add(translatePanningTransform);
            flipTransform = new ScaleTransform(1, 1);
            t.Children.Add(flipTransform);
            imageElement.RenderTransform = t;
            imageElement.RenderTransformOrigin = new Point(.5, .5);
        }

        public bool Zoom
        {
            get { return zoom; }
            set
            {
#if SILVERLIGHT
                if (zoom == false && value == true) {
                    DelayLoadUnscaledImage();
                }
#endif
                zoom = value;
                UpdateImageDisplay(true);
            }
        }

#if SILVERLIGHT
        // Because Silverlight only decodes images on the UI thread, 
        // we need to delay loading the unscaled bitmap until after
        // the animation is over.  Ideally, we would separate the download of 
        // the image from the decoding, but that takes a bit more effort to code...
        private void DelayLoadUnscaledImage()
        {
            var storyboard = new Storyboard();
            storyboard.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
            Storyboard.SetTarget(storyboard, this);
            var imageInfo = this.imageInfo;
            var unscaledSource = imageInfo.originalSource;
            storyboard.Completed +=
                (object sender, EventArgs e) => {
                    if (imageInfo != this.imageInfo)
                        return; //avoid race conditions
                    imageElementUnscaledSilverlight.Source = unscaledSource;
                };
            storyboard.Begin();
        }
#endif

        void  storyboard_Completed(object sender, EventArgs e)
        {
         	
        }

        public bool GrayscaleMode {
            get { return grayscaleMode; }
            set {
                grayscaleMode = value;
                UpdateGrayscale(true);
            }
        }

        public double Rotation {
            get { return rotation; }
            set {
                rotation = value;
                UpdateImageDisplay(true);
            }
        }

        public bool Flip
        {
            get { return flip; }
            set
            {
                flip = value;
                UpdateImageDisplay(true);
            }
        }

        // This is mostly a convenience for the PhotoGrid
        private ImageOrigin imageOrigin = null;
        public ImageOrigin ImageOrigin {
            get { return imageOrigin; }
            set {
                this.imageOrigin = value;
                Debug.Assert(imageInfo == null || imageInfo.Origin == imageOrigin);
                NotifyPropertyChanged("ImageOrigin");
            }
        }

        public ImageInfo ImageInfo
        {
            get { return imageInfo; }
            set {
                imageInfo = value;
                imageOrigin = (imageInfo == null)? null : imageInfo.Origin;
                UpdateImageDisplay(false);
                NotifyPropertyChanged("ImageInfo");
                NotifyPropertyChanged("ImageOrigin");
            }
        }

        public double CropMarkAspectRatio
        {
            get { return cropMarkAspectRatio; }
            set { 
                cropMarkAspectRatio = value;
                UpdateImageDisplay(true);
            }
        }

        // HACK: seems easier to implement INotifyPropertyChanged than make everything a dependency property
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(String info) {
            if (PropertyChanged != null) {
                var args = new PropertyChangedEventArgs(info);
                PropertyChanged(this, args);
            }
        }


        private void imageElement_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.Assert(false, "Image loading failed");
            // This should probably never happen, if it does it's most likely on Silverlight.  
            // In the WPF version, we load & decode the image on a background thread 
            // before we ever handed off to the Image element.
            // In the Silverlight version, the image file has been loaded by the time we give
            // it to Image, but it's not decoded.
        }

        public void CopySettingsFrom(ImageDisplay prototype) {
            this.zoom = prototype.zoom;
            this.grayscaleMode = prototype.grayscaleMode;
            this.rotation = prototype.rotation;
            this.flip = prototype.flip;

            this.zoomOffsetX = prototype.zoomOffsetX;
            this.zoomOffsetY = prototype.zoomOffsetY;
            UpdateImageDisplay(false);

            this.LostMouseCapture += new MouseEventHandler(ImageDisplay_LostMouseCapture);
        }

        void ImageDisplay_LostMouseCapture(object sender, MouseEventArgs e)
        {
            isMouseCaptured = false;
        }

        // reason for existing = so it doesn't do animations
        public void ResetRotation(ImageOrigin origin) {
                rotation = origin.Rotation;
                flip = origin.Flip;
        }

        //private double conversionFactor = 0;

        protected override Size ArrangeOverride(Size arrangeBounds) {
            Size res = base.ArrangeOverride(arrangeBounds);
            UpdateImageDisplay(false);

            // Canvas ignores Stretch = Stretch.Fill, & we need it
            foreach (UIElement e in Children) {
                if (e is Path) {
                    e.Arrange(new Rect(arrangeBounds));
                }
            }

            return res;
        }

        private void ImageDisplay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            this.Cursor = null;
            this.ReleaseMouseCapture();
            isMouseCaptured = false;
        }

        private void ImageDisplay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (zoom) {
                lastMouseX = e.GetPosition(this).X;
                lastMouseY = e.GetPosition(this).Y;
                this.Cursor = Cursors.Hand;
                this.CaptureMouse();
                isMouseCaptured = true;
            }
        }

        private void ImageDisplay_MouseMove(object sender, MouseEventArgs e) {
            if (this.isMouseCaptured) {
                zoomOffsetX += e.GetPosition(this).X - lastMouseX;
                zoomOffsetY += e.GetPosition(this).Y - lastMouseY;
                lastMouseX = e.GetPosition(this).X;
                lastMouseY = e.GetPosition(this).Y;
                UpdateImageDisplay(false);
            }
        }

        private void SetImageSourceProperty (bool animate) {
#if WPF
            BitmapSource bitmap = null;
            if (zoom)
                bitmap = imageInfo.originalSource;
            else
                bitmap = imageInfo.scaledSource;

            //Debug.Assert(bitmap!= null); 

            imageElement.Source = bitmap;
#else
            imageElement.Source = imageInfo.scaledSource;
            if (zoom) {
                if (animate) {
                    // do nothing, setting Zoom property kicked off the delay load
                } else {
                    imageElementUnscaledSilverlight.Source = imageInfo.bitmapSource;
                }
            } else {
                imageElementUnscaledSilverlight.Source = null;
            }
#endif
        }

        private void UpdateGrayscale(bool animate) {
            if (imageElementOld != null)
                imageElementOld.Opacity = 0;

            //imageElement.Opacity = 0;
            UpdateImageDisplay(false, this.imageElement, grayscaleMode);

            if (animate) {
                if (imageElementOld == null) {
                    imageElementOld = new Image();
                    this.Children.Add(imageElementOld);
                }

                //imageElementOld.Effect = null;
                UpdateImageDisplay(false, this.imageElementOld, !grayscaleMode);
                imageElementOld.Opacity = 1;
                Animate(imageElementOld, UIElement.OpacityProperty, (double)0);
                //var a = new DoubleAnimation();
                //a.From = 1;
                //a.To = ;0
                //a.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                //// not sure if this is strictly necessary but seems like a good idea to avoid animations accumulating forever
                //a.FillBehavior = FillBehavior.Stop;
                ////a.FillBehavior = FillBehavior.HoldEnd;
                //imageElementOld.Opacity = 0;
                //imageElementOld.BeginAnimation(UIElement.OpacityProperty, a);
            }
        }

        private void UpdateImageDisplay(bool animate)
        {
            if (imageElementOld != null)
                imageElementOld.Opacity = 0;
            UpdateImageDisplay(animate, this.imageElement, this.grayscaleMode);
        }

        private void UpdateImageDisplay(bool animate, Image imageElement, bool grayscaleMode) {
            if (imageInfo == null || !imageInfo.IsValid) {
                imageElement.Source = null;
                //imageElementUnscaledSilverlight.Source = null;
                return;
            }

            if (grayscaleMode) {
                if (effect == null)
                    effect = new GrayscaleEffect();
                imageElement.Effect = effect;
                //imageElement.Effect = new System.Windows.Media.Effects.DropShadowEffect();
            } else {
                imageElement.Effect = null;
            }

            FrameworkElement clientarea = this;
            SetImageSourceProperty(animate);

#if WPF
            // animation perf is lousy in the default scaling mode
            RenderOptions.SetBitmapScalingMode(imageElement, BitmapScalingMode.LowQuality);
#endif

            double clientwidth;
            double clientheight;
            GetSizeInPixels(clientarea, out clientwidth, out clientheight);

            double effectiveRotation = imageInfo.RotationDisplayAdjustment; // compensates for file rotation
            double conversionFactor = clientarea.ActualWidth / clientwidth;
            if (clientwidth == 0) conversionFactor = 1.0; // no one will ever notice but it's important to avoid passing around NaNs
            double rotatedClientWidth = clientwidth;
            double rotatedClientHeight = clientheight;

            if ((effectiveRotation % 180) != 0) {
                rotatedClientWidth = clientheight;
                rotatedClientHeight = clientwidth;
            }

            ChangeValue(rotateTransform, RotateTransform.AngleProperty, effectiveRotation, animate);
            Rect imageBoundsPixels = PositionImage(rotatedClientWidth, rotatedClientHeight, conversionFactor, (effectiveRotation % 180) != 0, animate);
            PositionCropMarks(clientwidth, clientheight, conversionFactor, animate, imageBoundsPixels);

            // BUG: may center on half pixel if rotated

            // rotating a 3D perspective plane would look better than animating 
            // scale, but this is a lot easier...
            double flipscale = (imageInfo.FlipDisplayAdjustment) ? -1 : 1; // imageInfo to compensate for file flip
            ChangeValue(flipTransform, ScaleTransform.ScaleXProperty, flipscale, animate);

            double scale;
            if (zoom) {
                scale = Math.Max(((double)imageInfo.PixelWidth) / rotatedClientWidth,
                    ((double)imageInfo.PixelHeight) / rotatedClientHeight);
            } else {
                scale = 1.0;
            }

            ChangeValue(scaleTransform, ScaleTransform.ScaleXProperty, scale, animate);
            ChangeValue(scaleTransform, ScaleTransform.ScaleYProperty, scale, animate);

            double offsetX = (zoom) ? zoomOffsetX : 0;
            double offsetY = (zoom) ? zoomOffsetY : 0;
            ChangeValue(translatePanningTransform, TranslateTransform.XProperty, offsetX, animate);
            ChangeValue(translatePanningTransform, TranslateTransform.YProperty, offsetY, animate);
        }

        // Convert to physical pixels
        public static void GetSizeInPixels(FrameworkElement element, out double width, out double height)
        {
#if WPF
            if (PresentationSource.FromVisual(element) == null) {
                // control isn't hooked up to visual tree so  who knows how big it is
                width = 0;
                height = 0;
            } else {
                Point upperleft = element.PointToScreen(new Point(0, 0));
                Point bottomright = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
                width = Math.Abs(bottomright.X - upperleft.X);
                height = Math.Abs(bottomright.Y - upperleft.Y);
            }
#else
            width = element.ActualWidth;
            height = element.ActualHeight;
#endif
        }

        private static bool IsZero(double num) {
            return Math.Abs(num) < 0.01;
        }

        private static bool IsWholeNumber(double num) {
            return IsZero(num - Math.Round(num));
        }

        // size & add margins to the image without misaligning pixels
        private Rect PositionImage(double clientwidth, double clientheight, double conversionFactor, 
            bool rotated, bool animate) {
            // virtual/physical

            Size bitmapDisplayPixels = imageInfo.SizePreservingAspectRatio((int)clientwidth, (int)clientheight);
            Debug.Assert(IsWholeNumber(bitmapDisplayPixels.Width));
            Debug.Assert(IsWholeNumber(bitmapDisplayPixels.Height));

            double leftoverwidth = clientwidth - bitmapDisplayPixels.Width;
            double leftoverheight = clientheight - bitmapDisplayPixels.Height;
            double shimX = leftoverwidth % 2;
            double shimY = leftoverheight % 2;
            Debug.Assert(leftoverheight <= 1 || leftoverwidth <= 1);

            double leftInPixels = (clientwidth - bitmapDisplayPixels.Width - shimX) / 2.0;
            double topInPixels = (clientheight - bitmapDisplayPixels.Height - shimY) / 2.0;
            Debug.Assert(IsWholeNumber(leftInPixels));
            Debug.Assert(IsWholeNumber(topInPixels));

            // Keep center of rotation same when top/left is changed
            if (rotated) {
                leftInPixels += (clientheight - clientwidth) / 2.0;
                topInPixels += (clientwidth - clientheight) / 2.0;
            }

            ChangeValue(imageElement, Canvas.LeftProperty, conversionFactor * leftInPixels, animate);
            ChangeValue(imageElement, Canvas.TopProperty, conversionFactor * topInPixels, animate);
            ChangeValue(imageElement, Image.WidthProperty, conversionFactor * bitmapDisplayPixels.Width, animate);
            ChangeValue(imageElement, Image.HeightProperty, conversionFactor * bitmapDisplayPixels.Height, animate);

            Rect bounds = new Rect(leftInPixels, topInPixels, bitmapDisplayPixels.Width, bitmapDisplayPixels.Height);
            if (rotated) {
                bounds = new Rect(topInPixels, leftInPixels, bitmapDisplayPixels.Height, bitmapDisplayPixels.Width);
            }
            return bounds;
        }

        private void PositionCropMarks(double clientwidth, double clientheight, double conversionFactor,
            bool animate, Rect imageBoundsPixels) {
            double cropAspect = this.CropMarkAspectRatio;
            if (cropAspect == 0) cropAspect = 1; // no divide by 0

            Size cropSize;
            if (imageBoundsPixels.Width > imageBoundsPixels.Height) {
                cropSize = ImageInfo.SizePreservingAspectRatio((int)imageBoundsPixels.Width,
                    (int)imageBoundsPixels.Height,
                    (int)(10000), (int)(10000 * cropAspect));
            } else {
                cropSize = ImageInfo.SizePreservingAspectRatio((int)imageBoundsPixels.Width,
                    (int)imageBoundsPixels.Height,
                    (int)(10000 * cropAspect), (int)(10000));
            }

            double cropLeft = (clientwidth - cropSize.Width) / 2;
            double cropTop = (clientheight - cropSize.Height) / 2;

            if (this.CropMarkAspectRatio > 0 && cropMark == null) {
                this.cropMark = new Rectangle();
                this.Children.Add(cropMark);
                this.cropMark.Stroke = Brushes.White;
                UpdateCropMarkBounds(conversionFactor, /*animate=*/ false, cropSize, cropLeft, cropTop);
                // need to do one non-animated before animation works
            }
            if (cropMark != null) {
                UpdateCropMarkBounds(conversionFactor, animate, cropSize, cropLeft, cropTop);
                ChangeValue(cropMark, OpacityProperty, (this.CropMarkAspectRatio == 0) ? 0.0 : 1.0, animate);
            }
        }

        private void UpdateCropMarkBounds(double conversionFactor, bool animate, Size cropSize, double cropLeft, double cropTop) {
            ChangeValue(cropMark, Canvas.LeftProperty, conversionFactor * cropLeft, animate);
            ChangeValue(cropMark, Canvas.TopProperty, conversionFactor * cropTop, animate);
            ChangeValue(cropMark, Image.WidthProperty, conversionFactor * cropSize.Width, animate);
            ChangeValue(cropMark, Image.HeightProperty, conversionFactor * cropSize.Height, animate);
        }

        // potentially animates it
        public static void ChangeValue(DependencyObject obj, DependencyProperty property, double newValue, bool animate) {
            if (animate) {
                Animate(obj, property, newValue);
            } else {
                obj.SetValue(property, newValue);
            }
        }

        public static void Animate(DependencyObject obj, DependencyProperty property, double newValue)
        {
            var a = new DoubleAnimation();
            a.From = (double)obj.GetValue(property);
            a.To = newValue;
            a.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                
            // animation seems to look better without acceleration, much to my surprise
            //a.AccelerationRatio = .3;
            //a.DecelerationRatio = .3;

            // not sure if this is strictly necessary but seems like a good idea to avoid animations accumulating forever
            a.FillBehavior = FillBehavior.Stop;
            obj.SetValue(property, newValue);

#if WPF
            var animatable = (IAnimatable)obj;
#else
            var animatable = obj;
#endif

            animatable.BeginAnimation(property, a);
        }
    }


#if SILVERLIGHT
    static class SilverlightHelpers
    {
        public static void BeginAnimation(this DependencyObject obj, DependencyProperty property, DoubleAnimation animation) {
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(storyboard, obj);
            Storyboard.SetTargetProperty(storyboard, new PropertyPath(property));
            storyboard.Begin();
        }
    }
#endif
}
