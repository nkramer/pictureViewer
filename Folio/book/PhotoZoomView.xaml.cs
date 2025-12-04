using Folio.Core;
using Folio.Shell;
using Folio.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
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

            Debug.WriteLine($"then -----------> {currentPageView.ActualWidth} x {currentPageView.ActualHeight}");

            // Set up the current page view
            currentPageView.Page = currentPage;
            currentPageView.IsFullscreenMode = false; // Don't want click handlers in zoom mode
            currentScale = (currentPageView.RenderTransform as TransformGroup).Children[0] as ScaleTransform;
            currentTranslate = (currentPageView.RenderTransform as TransformGroup).Children[1] as TranslateTransform;
            //x: Name = "nextTranslate"
            nextTranslate = nextPageView.RenderTransform as TranslateTransform;
        }

        private ScaleTransform currentScale;
        private TranslateTransform currentTranslate;
        private TranslateTransform nextTranslate;

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
                // Stay on same page, just pan to different photo
                PanToPhoto(newIndex, direction);
            }
        }

        private void PanToPhoto(int newPhotoIndex, int direction) {
            // Calculate the transform needed to focus on the new photo
            var photoRect = GetPhotoRect(newPhotoIndex);
            if (!photoRect.HasValue) return;

            var transform = CalculateFocusTransform(photoRect.Value);

            // Animate the pan
            AnimateTransform(currentScale, currentTranslate, transform.scale, transform.translateX, transform.translateY, () => {
                currentPhotoIndex = newPhotoIndex;
            });
        }

        private void NavigateToPage(int direction) {
            int currentPageIndex = book.Pages.IndexOf(currentPage);
            int newPageIndex = currentPageIndex + direction;

            if (newPageIndex < 0 || newPageIndex >= book.Pages.Count) {
                return;  // Can't navigate beyond book boundaries
            }

            var newPage = book.Pages[newPageIndex];
            int newPhotoIndex = direction > 0 ? 0 : newPage.Images.Count - 1;

            // Set up the next page view
            nextPageView.Page = newPage;
            nextPageView.IsFullscreenMode = false;
            nextPageView.Visibility = Visibility.Visible;

            // Calculate transform for the new page
            var photoRect = GetPhotoRect(newPhotoIndex, nextPageView);
            if (!photoRect.HasValue) {
                // Fallback if we can't get the rect
                nextPageView.Visibility = Visibility.Collapsed;
                return;
            }

            var transform = CalculateFocusTransform(photoRect.Value);

            // Set initial position for next page (off screen)
            double screenWidth = this.ActualWidth;
            nextTranslate.X = direction > 0 ? screenWidth : -screenWidth;

            // For page transitions, we don't scale the sliding page, just translate
            // Get current page's final X position
            double currentFinalX = direction > 0 ? -screenWidth : screenWidth;

            // Animate both pages
            var currentAnim = new DoubleAnimation(currentTranslate.X, currentFinalX, TimeSpan.FromMilliseconds(500));
            var nextAnim = new DoubleAnimation(nextTranslate.X, 0, TimeSpan.FromMilliseconds(500));

            currentAnim.FillBehavior = FillBehavior.Stop;
            nextAnim.FillBehavior = FillBehavior.Stop;

            nextAnim.Completed += (s, args) => {
                // Swap the views
                currentPageView.Page = newPage;
                currentPage = newPage;
                currentPhotoIndex = newPhotoIndex;

                // Apply the transform to the current view
                currentScale.ScaleX = transform.scale;
                currentScale.ScaleY = transform.scale;
                currentTranslate.X = transform.translateX;
                currentTranslate.Y = transform.translateY;

                // Reset next view
                nextPageView.Visibility = Visibility.Collapsed;
                nextTranslate.X = 0;
                nextTranslate.Y = 0;
            };

            currentTranslate.BeginAnimation(TranslateTransform.XProperty, currentAnim);
            nextTranslate.BeginAnimation(TranslateTransform.XProperty, nextAnim);
        }

        private void ExitZoomMode() {
            // bug: just animates, it doesn't exit the mode 
            AnimateToTransforms(currentScale, currentTranslate, 1, 0, 0);
        }

        // Public method to set the source rectangle for zoom animations
        public void SetSourceRect(Rect rect) {
            this.sourceRect = rect;
        }


        private void AnimateTransform(ScaleTransform scale, TranslateTransform translate, double targetScale, double targetX, double targetY, Action onComplete) {
            var scaleAnim = new DoubleAnimation(scale.ScaleX, targetScale, TimeSpan.FromMilliseconds(500));
            var translateXAnim = new DoubleAnimation(translate.X, targetX, TimeSpan.FromMilliseconds(500));
            var translateYAnim = new DoubleAnimation(translate.Y, targetY, TimeSpan.FromMilliseconds(500));

            scaleAnim.FillBehavior = FillBehavior.Stop;
            translateXAnim.FillBehavior = FillBehavior.Stop;
            translateYAnim.FillBehavior = FillBehavior.Stop;

            scaleAnim.Completed += (s, e) => {
                scale.ScaleX = targetScale;
                scale.ScaleY = targetScale;
                translate.X = targetX;
                translate.Y = targetY;
                onComplete?.Invoke();
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            translate.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
            translate.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
        }

        // Get the rectangle of a photo within the page view
        private Rect? GetPhotoRect(int photoIndex, PhotoPageView pageView = null) {
            if (pageView == null) pageView = currentPageView;

            pageView.UpdateLayout();

            // Find the DroppableImageDisplay for this photo index
            var imageDisplay = FindImageDisplay(pageView, photoIndex);
            if (imageDisplay == null) return null;

            // Get the position relative to the page view
            var transform = imageDisplay.TransformToAncestor(pageView);
            var topLeft = transform.Transform(new Point(0, 0));

            return new Rect(topLeft, new Size(imageDisplay.ActualWidth, imageDisplay.ActualHeight));
        }

        private DroppableImageDisplay FindImageDisplay(PhotoPageView pageView, int photoIndex) {
            return FindVisualChildren<DroppableImageDisplay>(pageView)
                .FirstOrDefault(d => d.ImageIndex == photoIndex);
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject obj) where T : DependencyObject {
            if (obj != null) {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) {
                    var child = VisualTreeHelper.GetChild(obj, i);
                    if (child is T t) {
                        yield return t;
                    }

                    foreach (var childOfChild in FindVisualChildren<T>(child)) {
                        yield return childOfChild;
                    }
                }
            }
        }

        // Calculate the transform needed to make a photo fill the screen
        private (double scale, double translateX, double translateY) CalculateFocusTransform(Rect photoRect) {
            double screenWidth = rootContainer.ActualWidth;
            double screenHeight = rootContainer.ActualHeight;

            // Calculate scale to fill the screen
            double scaleX = screenWidth / photoRect.Width;
            double scaleY = screenHeight / photoRect.Height;
            double scale = Math.Min(scaleX, scaleY); // Use the smaller scale to fit

            // Calculate the photo's center in page coordinates
            double photoCenterX = photoRect.X + photoRect.Width / 2;
            double photoCenterY = photoRect.Y + photoRect.Height / 2;

            // Calculate screen center
            double screenCenterX = screenWidth / 2;
            double screenCenterY = screenHeight / 2;

            // Calculate translation needed to center the photo
            // After scaling, the photo's center will be at (photoCenterX * scale, photoCenterY * scale)
            // We want to move it to screen center
            double translateX = screenCenterX - (photoCenterX * scale);
            double translateY = screenCenterY - (photoCenterY * scale);

            return (scale, translateX, translateY);
        }



        // Functions that actually work 

        public void ZoomIn(PhotoPageModel page, int photoIndex) {
            currentPage = page;
            currentPhotoIndex = photoIndex;
            currentPageView.Page = page;

            currentScale.ScaleX = 1;
            currentScale.ScaleY = 1;
            currentTranslate.X = 0;
            currentTranslate.Y = 0;
            //currentPageView.UpdateLayout();

            Rect photoBounds = GetPhotoBoundsInViewCoordinates(photoIndex);
            Rect destBounds = new Rect(0, 0, this.ActualWidth, this.ActualHeight);

            var (scale, translateX, translateY) = CalculateRectTransforms(photoBounds, destBounds);

            AnimateToTransforms(currentScale, currentTranslate, scale, translateX, translateY);
        }

        private Rect GetPhotoBoundsInViewCoordinates(int photoIndex) {
            var imageDisplay = FindImageDisplay(currentPageView, photoIndex);
            if (imageDisplay == null) {
                Debug.Fail($"Could not find image display for photo index {photoIndex}");
                return Rect.Empty;
            }

            currentPageView.UpdateLayout();

            var transform = imageDisplay.TransformToAncestor(this);
            var topLeft = transform.Transform(new Point(0, 0));
            var bottomRight = transform.Transform(new Point(imageDisplay.ActualWidth, imageDisplay.ActualHeight));

            return new Rect(topLeft, bottomRight);
        }

        private (double scale, double translateX, double translateY) CalculateRectTransforms(Rect sourceRect, Rect destRect) {
            double scaleX = destRect.Width / sourceRect.Width;
            double scaleY = destRect.Height / sourceRect.Height;
            double scale = Math.Min(scaleX, scaleY);

            double sourceCenterX = sourceRect.X + sourceRect.Width / 2;
            double sourceCenterY = sourceRect.Y + sourceRect.Height / 2;
            double destCenterX = destRect.X + destRect.Width / 2;
            double destCenterY = destRect.Y + destRect.Height / 2;

            double translateX = destCenterX - (sourceCenterX * scale);
            double translateY = destCenterY - (sourceCenterY * scale);

            return (scale, translateX, translateY);
        }

        private void AnimateToTransforms(ScaleTransform scale, TranslateTransform translate,
                                         double targetScale, double targetX, double targetY) {
            var scaleAnim = new DoubleAnimation(scale.ScaleX, targetScale, TimeSpan.FromMilliseconds(500));
            var translateXAnim = new DoubleAnimation(translate.X, targetX, TimeSpan.FromMilliseconds(500));
            var translateYAnim = new DoubleAnimation(translate.Y, targetY, TimeSpan.FromMilliseconds(500));

            scaleAnim.FillBehavior = FillBehavior.Stop;
            translateXAnim.FillBehavior = FillBehavior.Stop;
            translateYAnim.FillBehavior = FillBehavior.Stop;

            scaleAnim.Completed += (s, e) => {
                scale.ScaleX = targetScale;
                scale.ScaleY = targetScale;
                translate.X = targetX;
                translate.Y = targetY;
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            translate.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
            translate.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
        }

        void IScreen.Activate(ImageOrigin focus) {
        }

        void IScreen.Deactivate() {
        }
    }
}
