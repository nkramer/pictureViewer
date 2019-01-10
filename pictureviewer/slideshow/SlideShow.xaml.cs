using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.ComponentModel;
using Pictureviewer.Core;
using Pictureviewer.Utilities;

namespace pictureviewer
{
    public partial class SlideShow : UserControl, IScreen
    {
        private RootControl root;
        private ImageOrigin[] displaySet { get { return root.DisplaySet; } }
        private CommandHelper commands;
        private ImageOrigin typeaheadImage = null;
        private ImageLoader loader { get { return root.loader; } }
        private Storyboard shotclock;
        private int shotclockSpeed = 2;
        private bool paused = false;
        private bool pausedBeforeZoom = false;
        //private bool pixelPerfect = true;
        private ImageInfo displayedImageInfo;
        private Point previousMousePosition = new Point(); // used for panning in zooming mode

#if WPF
        private Window window;
        private ContextMenu contextmenu = new ContextMenu();
#endif

        public SlideShow(RootControl root)
        {
            InitializeComponent();
            this.root = root;
            root.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(root_PropertyChanged); // remember to unhook when done
            this.commands = new CommandHelper(this);

#if WPF
            this.ContextMenu = contextmenu;
            commands.contextmenu = contextmenu;

            this.window = Application.Current.Windows[0];

            this.Focusable = true;
            this.FocusVisualStyle = null;
#endif
            this.IsTabStop = true;

            shotclock = (Storyboard)this.Resources["shotclock"];
            shotclock.Completed += new EventHandler(shotclock_Completed);
            this.MouseMove += new MouseEventHandler(SlideShow_MouseMove);

            CreateCommands();
            commands.MergeMenus(root.commands);
            
            loader.Mode = LoaderMode.Slideshow;

            if (displaySet.Length == 0) {
                noImagesTextBlock.Visibility = Visibility.Visible;
            } else {
                noImagesTextBlock.Visibility = Visibility.Collapsed;
            }

            this.Loaded += new RoutedEventHandler(SlideShow_Loaded);
        }

        public void Unload()
        {
            // need to unhook events on shutdown so we get GC'ed
            root.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(root_PropertyChanged);
            shotclock.Completed -= new EventHandler(shotclock_Completed);
        }

        void SlideShow_Loaded(object sender, RoutedEventArgs e)
        {
            root_PropertyChanged(null, new PropertyChangedEventArgs(""));
        }

        void root_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (root.FocusedImage != null) {
                typeaheadImage = root.FocusedImage;
                loader.SetFocus(typeaheadImage);
                Debug.Assert(ActualHeight > 0 && ActualWidth > 0);
                if (ActualHeight > 0 && ActualWidth > 0) {
                    // bug: not sure why this isn't always the case
                    double clientwidth;
                    double clientheight;
                    ImageDisplay.GetSizeInPixels(clientarea, out clientwidth, out clientheight);
                    loader.BeginLoad(typeaheadImage, (int)clientwidth, (int)clientheight, ScalingBehavior.Full, 
                        loader_Loaded
                        );
                }
            }
        }


#if WPF
        void SlideShow_Deactivated(object sender, EventArgs e)
        {
            if (!App.EnableEscapeKey)
            {
                // try to reduce memory usage.  
                // I don't have good measurements on how much this helps, but a crude
                // test showed about 15-20M improvement (out of 110M).  And it's easy enough...
                oldImageDisplay.ImageInfo = null;
                if (loader != null)
                {
                    loader.ClearCache();
                    GC.Collect(1);
                }
            }
        }

        private void SlideShow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (loader != null)
                loader.Shutdown();
        }
        
#endif

        public ImageOrigin TypeaheadImage
        {
            get { return typeaheadImage; }
        }

        private void SlideShow_MouseMove(object sender, MouseEventArgs e)
        {
            if (previousMousePosition != e.GetPosition(this))
                DisplayToolbar(true);
            previousMousePosition = e.GetPosition(this);
        }

        private void DisplayToolbar(bool visible)
        {
            bool oldvalue = (this.Cursor == null);
            double opacity = (visible) ? 1 : 0;
            this.Cursor = (visible) ? null : Cursors.None;
            if (oldvalue != visible)
            {
                ImageDisplay.Animate(root.windowControls1, OpacityProperty, opacity);
                ImageDisplay.Animate(toolbar, OpacityProperty, opacity);
            }
            else
            {
                root.windowControls1.Opacity = opacity;
                toolbar.Opacity = opacity;
            }
        }

        private void shotclock_Completed(object sender, EventArgs e) {
            // ignore timers if we're still trying to catch up with keystrokes
            if (displayedImageInfo == null && typeaheadImage != null)
                return;

            if (displayedImageInfo != null && typeaheadImage != displayedImageInfo.Origin)
                return;

            UserMoveImage(1);
        }

        private void TogglePaused() {
            Paused = !paused;
        }

        public bool Paused {
            get { return paused; }
            set {
                paused = value;
                if (paused) {
                    shotclock.Pause();
                } else {
                    shotclock.Resume();
                }
                UpdateTextBlock();
            }
        }

        private void ToggleMetadataDisplay() {
            MetadataDisplay.Visibility
               = (MetadataDisplay.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UserJumpToImage(ImageOrigin origin) {
            typeaheadImage = origin;
            loader.SetFocus(origin);
            double clientwidth;
            double clientheight;
            ImageDisplay.GetSizeInPixels(clientarea, out clientwidth, out clientheight);
            loader.BeginLoad(origin, (int)clientwidth, (int)clientheight, ScalingBehavior.Full, loader_Loaded);
        }

        private void UserMoveImage(int increment) {
            ImageOrigin next = ImageOrigin.NextImage(displaySet, typeaheadImage, increment);
            UserJumpToImage(next);
        }

        //public void SelectDirectories(bool firstTime) {
        //    SlideShow slideshow = this;
        //    bool savedPauseState = slideshow.Paused;
        //    slideshow.Paused = true;
        //    try {
        //        fileListSource.SelectDirectories(firstTime,
        //            (SelectDirectoriesCompletedEventArgs args) => {
        //                root.SetDisplaySet(args.imageOrigins, args.initialFocus);
        //                typeaheadImage = args.initialFocus;
        //                displayedImageInfo = null;
        //                //ResetLoader();
        //            }
        //            );

        //    } finally {
        //        slideshow.Paused = savedPauseState;
        //    }
        //}

        private void loader_PreloadComplete(object sender, LoadedEventArgs args) {
            // shotclockRectangle.Fill = new SolidColorBrush(Colors.Green);
#if SILVERLIGHT
            // Hack to get round the fact that Silverlight can't decode images on the background thread.
            // We're still blocking the UI, but we try to do it when it's less likely the user wants to do something
            //SilverlightPreloadHack.Source = args.ImageInfo.bitmapSource;
#endif
        }

        private void loader_Loaded(ImageInfo loadedInfo) {
            // see if old image was selected and needs to be saved
            if (displayedImageInfo != null) {
                UpdateTargetDirectory();
            }

            if (loadedInfo.Origin != typeaheadImage) {
                // can get callbacks out of order
                return;
            }

            // UNDONE -- what to do with a load that happened before we changed directories, & is writing to the old directory

            var oldImageInfo = displayedImageInfo;
            displayedImageInfo = loadedInfo;
            oldImageDisplay.CopySettingsFrom(imageDisplay);
            AnimateSlideTransition(oldImageInfo, displayedImageInfo);

            UpdateTextBlock();

            if (root.IsFullScreen) {
                DisplayToolbar(false);
            }

            //shotclockRectangle.Fill = new SolidColorBrush(Colors.White);
            shotclock.Stop(); // in wpf, if we don't stop the animation before you modify it, you'll later get a completed event for the original unmodified storyboard
            shotclock.Duration = new Duration(new TimeSpan(0, 0, shotclockSpeed));
            shotclock.Children[0].Duration = shotclock.Duration;
            shotclock.Begin();
            if (paused) {
                shotclock.Pause();
            }
        }

        private void AnimateSlideTransition(ImageInfo oldImageInfo, ImageInfo newImageInfo) {
            var distanceBetweenImages = clientarea.ActualWidth;

            if (oldImageInfo != null) {
                oldImageDisplay.ImageInfo = oldImageInfo;
                oldImageDisplay.Opacity = 1;
                ImageDisplay.ChangeValue(oldImageDisplay, OpacityProperty,
                    0, true);
            }
            imageDisplay.ResetRotation(newImageInfo.Origin);
            imageDisplay.ImageInfo = newImageInfo;
        }

        private void UpdateTargetDirectory()
        {
            // bug - really ought to tie this to selectionchanged so file copy happens in Grid mode too
            //root.fileListSource.UpdateTargetDirectory(displayedImageInfo.Origin);
        }

        private void UpdateTextBlock() {
            var bitmap = (this.displayedImageInfo != null) ? this.displayedImageInfo.originalSource : null;

            if (bitmap == null) {
                noImagesTextBlock.Text = "[invalid image]";
                noImagesTextBlock.Visibility = Visibility.Visible;
            } else {
                noImagesTextBlock.Visibility = Visibility.Collapsed;
            }

            string text;
            bool isSelected;
            if (displayedImageInfo == null) {
                text = "[no image]";
                isSelected = false;
            } else {
                text = ImageDescription(displayedImageInfo);
                if (!displayedImageInfo.IsValid) {
                    isSelected = false;
                } else {
                    isSelected = displayedImageInfo.Origin.IsSelected;
                }
            }

            textblock.Text = text;
            if (isSelected) {
                textblock.FontWeight = FontWeights.Bold;
                textblock.Foreground = new SolidColorBrush(Color.FromArgb(255, 0xDD, 0xB6, 0xB6));// Brushes.LightSalmon
            } else {
                textblock.FontWeight = FontWeights.Normal;
                textblock.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private string ImageDescription(ImageInfo displayedImageInfo)
        {
            string text;
            string separator = "\n";
            int index = ImageOrigin.GetIndex(displaySet, displayedImageInfo.Origin);
            if (!displayedImageInfo.IsValid) {
                text = "[invalid file]";
                text += separator;
                text += "" + (index + 1) + " of " + displaySet.Length;
                text += separator;
                text += displayedImageInfo.Origin.DisplayName;
            } else {
                text = "" + (index + 1) + " of " + displaySet.Length;
                if (paused) {
                    text += " [paused]";
                }
                text += separator;
                text += displayedImageInfo.ImageMetadataText;
            }
            return text;
        }

        void IScreen.Activate(ImageOrigin focus) {
            
        }

        void IScreen.Deactivate() {
            Unload();
        }
    }
}
