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
        private struct TransformValues {
            public double Scale;
            public double TranslateX;
            public double TranslateY;

            public TransformValues(double scale, double translateX, double translateY) {
                Scale = scale;
                TranslateX = translateX;
                TranslateY = translateY;
            }
        }

        private CommandHelper commands;
        private BookModel book;
        private PhotoPageModel currentPage;
        private int currentPhotoIndex;

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

            // For some reason you can't put x:Names on these things 
            currentScale = (transformHolder.RenderTransform as TransformGroup).Children[0] as ScaleTransform;
            currentTranslate = (transformHolder.RenderTransform as TransformGroup).Children[1] as TranslateTransform;
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
                //NavigateToPage(-1);
            } else if (newIndex >= currentPage.Images.Count) {
                // Move to next page
                //NavigateToPage(1);
            } else {
                // Stay on same page, just pan to different photo
                PanToPhoto(newIndex);
            }
        }

        private void PanToPhoto(int newPhotoIndex) {
            ZoomIn(currentPage, newPhotoIndex);
            currentPhotoIndex = newPhotoIndex;
        }

        private void ExitZoomMode() {
            // bug: just animates, it doesn't exit the mode
            AnimateToTransforms(currentScale, currentTranslate, new TransformValues(1, 0, 0));
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

        // Functions that actually work 

        public void ZoomIn(PhotoPageModel page, int photoIndex) {
            currentPage = page;
            currentPhotoIndex = photoIndex;
            currentPageView.Page = page;

            Rect photoBounds = GetPhotoBoundsInViewCoordinates(photoIndex);
            Rect destBounds = new Rect(0, 0, this.ActualWidth, this.ActualHeight);
            TransformValues transforms = CalculateRectTransforms(photoBounds, destBounds);
            AnimateToTransforms(currentScale, currentTranslate, transforms);
        }

        private Rect GetPhotoBoundsInViewCoordinates(int photoIndex) {
            var imageDisplay = FindImageDisplay(currentPageView, photoIndex);
            if (imageDisplay == null) {
                Debug.Fail($"Could not find image display for photo index {photoIndex}");
                return Rect.Empty;
            }

            currentPageView.UpdateLayout();

            var transform = imageDisplay.TransformToAncestor(rootContainer);
            var topLeft = transform.Transform(new Point(0, 0));
            var bottomRight = transform.Transform(new Point(imageDisplay.ActualWidth, imageDisplay.ActualHeight));

            return new Rect(topLeft, bottomRight);
        }

        private TransformValues CalculateRectTransforms(Rect sourceRect, Rect destRect) {
            double scaleX = destRect.Width / sourceRect.Width;
            double scaleY = destRect.Height / sourceRect.Height;
            double scale = Math.Min(scaleX, scaleY);

            double sourceCenterX = sourceRect.X + sourceRect.Width / 2;
            double sourceCenterY = sourceRect.Y + sourceRect.Height / 2;
            double destCenterX = destRect.X + destRect.Width / 2;
            double destCenterY = destRect.Y + destRect.Height / 2;

            double translateX = destCenterX - (sourceCenterX * scale);
            double translateY = destCenterY - (sourceCenterY * scale);

            return new TransformValues(scale, translateX, translateY);
        }

        private void AnimateToTransforms(ScaleTransform scale, TranslateTransform translate, TransformValues target) {
            var scaleAnim = new DoubleAnimation(target.Scale, TimeSpan.FromMilliseconds(500), FillBehavior.HoldEnd);
            var translateXAnim = new DoubleAnimation(target.TranslateX, TimeSpan.FromMilliseconds(500), FillBehavior.HoldEnd);
            var translateYAnim = new DoubleAnimation(target.TranslateY, TimeSpan.FromMilliseconds(500), FillBehavior.HoldEnd);

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
