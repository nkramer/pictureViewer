using Folio.Core;
using Folio.Shell;
using Folio.Utilities;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Folio.Book {
    public partial class PhotoZoomView : UserControl, IScreen {
        private CommandHelper commands;
        private BookModel book;
        private PhotoPageModel currentPage;
        private int currentPhotoIndex;
        private Rect? sourceRect = null;  // For zoom in/out animations
        private Action<PhotoPageModel, int> onExit = null;  // Callback when exiting zoom mode

        public PhotoZoomView(BookModel book, PhotoPageModel page, int photoIndex) {
            InitializeComponent();

            this.book = book;
            this.currentPage = page;
            this.currentPhotoIndex = photoIndex;

            this.commands = new CommandHelper(this);
            CreateCommands();

            this.Loaded += PhotoZoomView_Loaded;
            this.Focusable = true;
        }

        void PhotoZoomView_Loaded(object sender, RoutedEventArgs e) {
            this.Focus();
            LoadCurrentPhoto(false);
        }

        private void CreateCommands() {
            Command command;

            // Escape to exit zoom mode
            command = new Command();
            command.Key = Key.Escape;
            command.Text = "Exit zoom";
            command.Execute += delegate () {
                ExitZoomMode();
            };
            commands.AddCommand(command);

            // Right arrow - next photo
            command = new Command();
            command.Key = Key.Right;
            command.Text = "Next photo";
            command.Execute += delegate () {
                NavigatePhotos(1);
            };
            commands.AddCommand(command);

            // Left arrow - previous photo
            command = new Command();
            command.Key = Key.Left;
            command.Text = "Previous photo";
            command.Execute += delegate () {
                NavigatePhotos(-1);
            };
            commands.AddCommand(command);
        }

        private void LoadCurrentPhoto(bool animate) {
            var imageOrigin = GetCurrentImageOrigin();
            if (imageOrigin == null) return;

            // Load the image
            double clientWidth, clientHeight;
            ImageDisplay.GetSizeInPhysicalPixels(currentImageDisplay, out clientWidth, out clientHeight);

            RootControl.Instance.loader.BeginLoad(
                new LoadRequest(imageOrigin, (int)clientWidth, (int)clientHeight, ScalingBehavior.Small),
                (info) => {
                    if (info != null && info.Origin == imageOrigin) {
                        currentImageDisplay.ImageInfo = info;
                    }
                }
            );
        }

        private ImageOrigin GetCurrentImageOrigin() {
            if (currentPage == null || currentPhotoIndex < 0 || currentPhotoIndex >= currentPage.Images.Count) {
                return null;
            }
            return currentPage.Images[currentPhotoIndex];
        }

        private void NavigatePhotos(int direction) {
            int newIndex = currentPhotoIndex + direction;

            // Check if we need to move to next/previous page
            if (newIndex < 0) {
                // Move to previous page
                NavigateToPage(-1);
            } else if (newIndex >= currentPage.Images.Count) {
                // Move to next page
                NavigateToPage(1);
            } else {
                // Stay on same page, just move to different photo
                SlideToPhoto(newIndex, direction);
            }
        }

        private void SlideToPhoto(int newPhotoIndex, int direction) {
            var newImageOrigin = currentPage.Images[newPhotoIndex];
            if (newImageOrigin == null) return;

            // Load the new photo into the next display
            double clientWidth, clientHeight;
            ImageDisplay.GetSizeInPhysicalPixels(nextImageDisplay, out clientWidth, out clientHeight);

            RootControl.Instance.loader.BeginLoad(
                new LoadRequest(newImageOrigin, (int)clientWidth, (int)clientHeight, ScalingBehavior.Small),
                (info) => {
                    if (info != null && info.Origin == newImageOrigin) {
                        nextImageDisplay.ImageInfo = info;
                        AnimatePhotoSlide(newPhotoIndex, direction);
                    }
                }
            );
        }

        private void AnimatePhotoSlide(int newPhotoIndex, int direction) {
            // Show both containers
            nextPhotoContainer.Visibility = Visibility.Visible;

            // Set initial positions
            double screenWidth = this.ActualWidth;
            currentTransform.X = 0;
            nextTransform.X = direction > 0 ? screenWidth : -screenWidth;

            // Animate current photo off screen
            var currentAnim = new DoubleAnimation();
            currentAnim.From = 0;
            currentAnim.To = direction > 0 ? -screenWidth : screenWidth;
            currentAnim.Duration = new Duration(TimeSpan.FromMilliseconds(500));
            currentAnim.FillBehavior = FillBehavior.Stop;

            // Animate next photo onto screen
            var nextAnim = new DoubleAnimation();
            nextAnim.From = direction > 0 ? screenWidth : -screenWidth;
            nextAnim.To = 0;
            nextAnim.Duration = new Duration(TimeSpan.FromMilliseconds(500));
            nextAnim.FillBehavior = FillBehavior.Stop;

            nextAnim.Completed += (s, e) => {
                // Swap the displays
                currentImageDisplay.ImageInfo = nextImageDisplay.ImageInfo;
                currentTransform.X = 0;
                nextTransform.X = 0;
                nextPhotoContainer.Visibility = Visibility.Collapsed;
                currentPhotoIndex = newPhotoIndex;
            };

            currentTransform.BeginAnimation(TranslateTransform.XProperty, currentAnim);
            nextTransform.BeginAnimation(TranslateTransform.XProperty, nextAnim);
        }

        private void NavigateToPage(int direction) {
            int currentPageIndex = book.Pages.IndexOf(currentPage);
            int newPageIndex = currentPageIndex + direction;

            if (newPageIndex < 0 || newPageIndex >= book.Pages.Count) {
                return;  // Can't navigate beyond book boundaries
            }

            var newPage = book.Pages[newPageIndex];
            int newPhotoIndex = direction > 0 ? 0 : newPage.Images.Count - 1;

            // Animate transition to the new page
            // First slide the current photo off screen, then pop this screen and let the page view slide in
            double screenWidth = this.ActualWidth;

            var currentAnim = new DoubleAnimation();
            currentAnim.From = 0;
            currentAnim.To = direction > 0 ? -screenWidth : screenWidth;
            currentAnim.Duration = new Duration(TimeSpan.FromMilliseconds(500));
            currentAnim.FillBehavior = FillBehavior.Stop;

            currentAnim.Completed += (s, e) => {
                // Update the book's selected page and exit zoom mode
                book.SelectedPage = newPage;
                RootControl.Instance.PopScreen();
            };

            currentTransform.BeginAnimation(TranslateTransform.XProperty, currentAnim);
        }

        private void ExitZoomMode() {
            if (sourceRect.HasValue) {
                // Animate zoom out to the source position
                AnimateZoomOut(() => {
                    onExit?.Invoke(currentPage, currentPhotoIndex);
                    RootControl.Instance.PopScreen();
                });
            } else {
                // No source rect, just exit immediately
                onExit?.Invoke(currentPage, currentPhotoIndex);
                RootControl.Instance.PopScreen();
            }
        }

        // Public method to set the source rectangle for zoom animations
        public void SetSourceRect(Rect rect) {
            this.sourceRect = rect;
        }

        // Public method to set the exit callback
        public void SetExitCallback(Action<PhotoPageModel, int> callback) {
            this.onExit = callback;
        }

        // Public method to trigger zoom-in animation from the source rect
        public void AnimateZoomIn() {
            if (!sourceRect.HasValue) {
                return;  // No animation if no source rect
            }

            //var rect = sourceRect.Value;

            //// Set up initial state - photo at source position and size
            //var scaleTransform = new ScaleTransform(rect.Width / this.ActualWidth, rect.Height / this.ActualHeight);
            //var translateTransform = new TranslateTransform(
            //    rect.Left + rect.Width / 2 - this.ActualWidth / 2,
            //    rect.Top + rect.Height / 2 - this.ActualHeight / 2
            //);

            //var transformGroup = new TransformGroup();
            //transformGroup.Children.Add(scaleTransform);
            //transformGroup.Children.Add(translateTransform);
            //currentImageDisplay.RenderTransform = transformGroup;

            //// Animate to full screen
            //var scaleXAnim = new DoubleAnimation(rect.Width / this.ActualWidth, 1.0, TimeSpan.FromMilliseconds(500));
            //var scaleYAnim = new DoubleAnimation(rect.Height / this.ActualHeight, 1.0, TimeSpan.FromMilliseconds(500));
            //var translateXAnim = new DoubleAnimation(
            //    rect.Left + rect.Width / 2 - this.ActualWidth / 2,
            //    0,
            //    TimeSpan.FromMilliseconds(500)
            //);
            //var translateYAnim = new DoubleAnimation(
            //    rect.Top + rect.Height / 2 - this.ActualHeight / 2,
            //    0,
            //    TimeSpan.FromMilliseconds(500)
            //);

            //scaleXAnim.FillBehavior = FillBehavior.Stop;
            //scaleYAnim.FillBehavior = FillBehavior.Stop;
            //translateXAnim.FillBehavior = FillBehavior.Stop;
            //translateYAnim.FillBehavior = FillBehavior.Stop;

            //scaleXAnim.Completed += (s, e) => {
            //    // Reset to the currentTransform after animation
            //    currentImageDisplay.RenderTransform = currentTransform;
            //    currentTransform.X = 0;
            //    currentTransform.Y = 0;
            //};

            //scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            //scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            //translateTransform.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
            //translateTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
        }

        private void AnimateZoomOut(Action onComplete) {
            if (!sourceRect.HasValue) {
                onComplete?.Invoke();
                return;
            }

            var rect = sourceRect.Value;

            // Set up transform group (starting from current state)
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var translateTransform = new TranslateTransform(currentTransform.X, currentTransform.Y);

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            currentImageDisplay.RenderTransform = transformGroup;

            // Animate to source position and size
            var scaleXAnim = new DoubleAnimation(1.0, rect.Width / this.ActualWidth, TimeSpan.FromMilliseconds(500));
            var scaleYAnim = new DoubleAnimation(1.0, rect.Height / this.ActualHeight, TimeSpan.FromMilliseconds(500));
            var translateXAnim = new DoubleAnimation(
                currentTransform.X,
                rect.Left + rect.Width / 2 - this.ActualWidth / 2,
                TimeSpan.FromMilliseconds(500)
            );
            var translateYAnim = new DoubleAnimation(
                currentTransform.Y,
                rect.Top + rect.Height / 2 - this.ActualHeight / 2,
                TimeSpan.FromMilliseconds(500)
            );

            scaleXAnim.FillBehavior = FillBehavior.Stop;
            scaleYAnim.FillBehavior = FillBehavior.Stop;
            translateXAnim.FillBehavior = FillBehavior.Stop;
            translateYAnim.FillBehavior = FillBehavior.Stop;

            scaleXAnim.Completed += (s, e) => {
                onComplete?.Invoke();
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            translateTransform.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
        }

        void IScreen.Activate(ImageOrigin focus) {
        }

        void IScreen.Deactivate() {
        }
    }
}
