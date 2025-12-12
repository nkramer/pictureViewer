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
    // We should probably move this logic BookViewerFullscreen.
    // Might even fix a known bug: the first image you zoom into doesn't always zoom into
    // the right dimensions. But if you arrow around to other images then go back to the first
    // one, it uses the right bounds. I think what's happening is that first image is being
    // navigated to before it's done loading the image, which means if the aspect ratio is
    // different from the page template, it animating to the wrong size. 
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

            // Set up the current page view
            currentPageView.Page = currentPage;
            currentPageView.IsFullscreenMode = false; // Don't want click handlers in zoom mode

            // For some reason you can't put x:Names on these things 
            currentScale = (transformHolder.RenderTransform as TransformGroup).Children[0] as ScaleTransform;
            currentTranslate = (transformHolder.RenderTransform as TransformGroup).Children[1] as TranslateTransform;
        }

        private ScaleTransform currentScale;
        private TranslateTransform currentTranslate;

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

            int totalElements = GetTotalElementCount();

            if (newIndex < 0 || newIndex >= totalElements) {
                ExitZoomMode();
            } else {
                PanToElement(newIndex);
            }
        }

        private int GetTotalElementCount() {
            int captionCount = FindVisualChildren<CaptionView>(currentPageView).Count();
            return currentPage.Images.Count + captionCount;
        }

        private void PanToElement(int elementIndex) {
            FrameworkElement element = GetElementByIndex(elementIndex);
            if (element != null) {
                ZoomToElement(element);
                currentPhotoIndex = elementIndex;
            }
        }

        // hack, should create a proper API on PhotoPageView
        private FrameworkElement GetElementByIndex(int index) {
            if (index < currentPage.Images.Count) {
                return FindImageDisplay(currentPageView, index);
            } else {
                int captionIndex = index - currentPage.Images.Count;
                return FindVisualChildren<CaptionView>(currentPageView).ElementAtOrDefault(captionIndex);
            }
        }

        private void ExitZoomMode() {
            bool once = false;
            AnimateToTransforms(currentScale, currentTranslate, new TransformValues(1, 0, 0), () => {
                // I don't know how you can get more than one completed event out of one animation, but apparently you can 
                if (!once)
                    RootControl.Instance.PopScreen();
                once = true;
            });
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

        public void ZoomIn(PhotoPageModel page, int photoIndex) {
            currentPage = page;
            currentPhotoIndex = photoIndex;
            currentPageView.Page = page;

            var imageDisplay = FindImageDisplay(currentPageView, photoIndex);
            if (imageDisplay != null) {
                ZoomToElement(imageDisplay);
            }
        }

        public void ZoomToElement(FrameworkElement element) {
            Rect elementBounds = GetElementBoundsInViewCoordinates(element);
            Rect destBounds = new Rect(0, 0, this.ActualWidth, this.ActualHeight);
            TransformValues transforms = CalculateRectTransforms(elementBounds, destBounds);
            AnimateToTransforms(currentScale, currentTranslate, transforms);
        }

        private Rect GetPhotoBoundsInViewCoordinates(int photoIndex) {
            var imageDisplay = FindImageDisplay(currentPageView, photoIndex);
            if (imageDisplay == null) {
                Debug.Fail($"Could not find image display for photo index {photoIndex}");
                return Rect.Empty;
            }

            return GetElementBoundsInViewCoordinates(imageDisplay);
        }

        private Rect GetElementBoundsInViewCoordinates(FrameworkElement element) {
            currentPageView.UpdateLayout();

            var transform = element.TransformToAncestor(rootContainer);
            var topLeft = transform.Transform(new Point(0, 0));
            var bottomRight = transform.Transform(new Point(element.ActualWidth, element.ActualHeight));
            Debug.WriteLine($"Element bounds in view coordinates: TopLeft({topLeft.X}, {topLeft.Y}), BottomRight({bottomRight.X}, {bottomRight.Y})");

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

        private void AnimateToTransforms(ScaleTransform scale, TranslateTransform translate, TransformValues target, Action? onComplete = null) {
            var scaleAnim = new DoubleAnimation(target.Scale, TimeSpan.FromMilliseconds(500), FillBehavior.HoldEnd);
            var translateXAnim = new DoubleAnimation(target.TranslateX, TimeSpan.FromMilliseconds(500), FillBehavior.HoldEnd);
            var translateYAnim = new DoubleAnimation(target.TranslateY, TimeSpan.FromMilliseconds(500), FillBehavior.HoldEnd);

            if (onComplete != null) {
                scaleAnim.Completed += (s, e) => onComplete();
            }

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
