using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;

namespace Pictureviewer.Core {
    // 
    public enum ScalingBehavior {
        Thumbnail, // the JPEG thumbnail field. The main pixels of the JPEG are never read.
        Small, // requested resolution only
        
        // A scaledSource that matches the screen resolution, as well as 
        //the originalSource at the image's native resolution
        Full, 
        
        Print, // A scaledSource that has a resolution equal to the originalSource.
    }

    // All the metadata about an image, along with its pixels.
    public partial class ImageInfo {
        // this field can be changed after construction by the ImageDecoder class
#if WPF
        // while both WPF & Silverlight has BitmapImage, in the WPF version we 
        // need the more general BitmapSource, because in some code paths 
        // we use various things that aren't quite BitmapImages.
        public BitmapSource originalSource;
        public BitmapSource scaledSource;
#else
        public BitmapImage bitmapSource;
        public BitmapImage scaledSource;
#endif

        private bool isValid = true;
        public bool IsValid { get { return isValid; } }

        private readonly ImageOrigin origin;
        public ImageOrigin Origin { get { return origin; } }

        public readonly Ratio focalLength;
        public readonly Ratio isospeed;
        public readonly Ratio exposureTime;
        public readonly Ratio whiteBalance;
        public readonly Ratio fstop;
        public readonly Ratio exposureBias;  // f-stop adjustment
        private double fileRotation = 0; // conceptionally readonly
        private bool fileFlip = false; // conceptionally readonly

        public double RotationDisplayAdjustment {
            get { return origin.Rotation - fileRotation; }
        }

        public bool FlipDisplayAdjustment {
            get { return origin.Flip ^ fileFlip; } //xor
        }

        private int pixelHeight;

        public int PixelHeight {
            get { return pixelHeight; }
        }

        private int pixelWidth;

        public int PixelWidth {
            get { return pixelWidth; }
            set { pixelWidth = value; }
        }

        private ImageInfo(ImageOrigin origin) {
            this.origin = origin;
            this.isValid = false;
        }

        public static ImageInfo CreateInvalidImage(ImageOrigin file) {
            var info = new ImageInfo(file);
            info.isValid = false;
            return info;
        }

        public static Size SizePreservingAspectRatio(int maxWidth, int maxHeight, int originalWidth, int originalHeight) {
            if (originalHeight == 0 || originalWidth == 0)
                return new Size(0, 0);

            int height = originalHeight;
            int width = originalWidth;

            if (maxWidth > 0 && maxHeight > 0) {
                double imageAspectRatio = ((double)height) / width;
                double screenAspectRatio = ((double)maxHeight) / maxWidth;
                if (imageAspectRatio > screenAspectRatio) {
                    height = maxHeight;
                    width = (int)Math.Round(height / imageAspectRatio);
                } else {
                    width = maxWidth;
                    height = (int)Math.Round(width * imageAspectRatio);
                }
            }

            return new Size(width, height);
        }

        // values are in pixels not virtual pixels
        public Size SizePreservingAspectRatio(int maxWidth, int maxHeight) {
            return SizePreservingAspectRatio(maxWidth, maxHeight, this.PixelWidth, this.PixelHeight);
        }

        // will only return the filename for thumbnails -- we don't have metadata
        public string ImageMetadataText {
            get {
                ImageInfo displayedImageInfo = this;
                string text = "";
                string separator = "\n";
                text += displayedImageInfo.Origin.DisplayName;
                string smallSeparator = " ";

                if (displayedImageInfo.exposureTime != null) {
                    if (displayedImageInfo.exposureTime.IsValid) {
                        text += separator;
                        text += displayedImageInfo.exposureTime + "s";
                    }

                    if (displayedImageInfo.exposureBias.IsValid && displayedImageInfo.exposureBias.numerator != 0) {
                        text += smallSeparator;
                        if (displayedImageInfo.exposureBias.numerator > 0)
                            text += "+";
                        if (displayedImageInfo.exposureBias.numerator % displayedImageInfo.exposureBias.denominator == 0)
                            text += displayedImageInfo.exposureBias.numerator / displayedImageInfo.exposureBias.denominator;
                        else
                            text += displayedImageInfo.exposureBias;
                        text += "stop";
                    }

                    if (displayedImageInfo.focalLength.IsValid && displayedImageInfo.focalLength.denominator == 1) {
                        text += smallSeparator;
                        text += displayedImageInfo.focalLength + "mm";
                    }

                    if (displayedImageInfo.fstop.IsValid) {
                        text += smallSeparator;
                        text += "f" + (((float)displayedImageInfo.fstop.numerator) / displayedImageInfo.fstop.denominator);
                    }

                    if (displayedImageInfo.isospeed.IsValid && displayedImageInfo.isospeed.numerator > 0) {
                        text += smallSeparator;
                        text += "ISO" + displayedImageInfo.isospeed.numerator;
                    }
                }
                return text;
            }
        }
    }
}
