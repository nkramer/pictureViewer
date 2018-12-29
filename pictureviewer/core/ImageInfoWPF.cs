using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;
using System.Windows.Controls;

namespace Pictureviewer.Core
{
    // The parts of ImageInfo that can only be implemented in WPF (not Silverlight).
    public partial class ImageInfo
    {
        public static ImageInfo Load(ImageOrigin file, int displayWidth, int displayHeight, ImageResolution resolution)
        {
            return ImageDecoder.Decode(file, displayWidth, displayHeight, resolution);
        }

        // Makes the protected VisualBitmapScalingMode property into a public property
        internal class DrawingVisualWorkaround : DrawingVisual
        {
            public BitmapScalingMode BitmapScalingMode
            {
                get { return VisualBitmapScalingMode; }
                set { VisualBitmapScalingMode = value; }
            }
        }

        private ImageInfo(BitmapSource bitmapSource, BitmapMetadata metadata, ImageOrigin origin)
        {
            this.originalSource = bitmapSource;
            this.origin = origin;
            this.pixelHeight = bitmapSource.PixelHeight;
            this.pixelWidth = bitmapSource.PixelWidth;

            // see http://www.exif.org/Exif2-1.PDF
            // and http://msdn.microsoft.com/en-us/library/ee719904(VS.85).aspx#_jpeg_metadata
            // and http://www.codeproject.com/Articles/66328/Enumerating-all-of-the-Metadata-Tags-in-an-Image-F
            // and http://msdn.microsoft.com/en-us/library/windows/desktop/ee720018(v=vs.85).aspx
            // and http://csgraphicslib.googlecode.com/svn-history/r18/trunk/GraphicsLib/ExifInformation.pas
            // and http://www.exiv2.org/tags-canon.html
            focalLength = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=37386}");
            isospeed = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=34855}");
            exposureTime = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=33434}");
            whiteBalance = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=41987}");
            fstop = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=33437}");
            exposureBias = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=37380}");

            InitRotationAndFlip(metadata);

            if (metadata != null) {
                object v = metadata.GetQuery("/app1/ifd/{ushort=271}");
                object v2 = metadata.GetQuery("/app1/ifd/{ushort=272}");
                object v3 = metadata.GetQuery("/xmp/MicrosoftPhoto:LensModel");
                object v4 = metadata.GetQuery("/app1/ifd/exif/{ushort=42036}");
                //object v = metadata.GetQuery("/app1/ifd/{ushort=149}");
                 CaptureMetadata(metadata, "");

              }
        }

        // http://www.codeproject.com/Articles/66328/Enumerating-all-of-the-Metadata-Tags-in-an-Image-F
        class RawMetadataItem {
            public String location;
            public Object value;
            public override string ToString() {
                return location + "=" + value;
            }
        }

        List<RawMetadataItem> RawMetadataItems = new List<RawMetadataItem>();

        private void CaptureMetadata(ImageMetadata imageMetadata, string query) {
            BitmapMetadata bitmapMetadata = imageMetadata as BitmapMetadata;

            if (bitmapMetadata != null) {
                foreach (string relativeQuery in bitmapMetadata) {
                    string fullQuery = query + relativeQuery;
                    object metadataQueryReader = bitmapMetadata.GetQuery(relativeQuery);
                    RawMetadataItem metadataItem = new RawMetadataItem();
                    metadataItem.location = fullQuery;
                    metadataItem.value = metadataQueryReader;
                    RawMetadataItems.Add(metadataItem);
                    BitmapMetadata innerBitmapMetadata = metadataQueryReader as BitmapMetadata;
                    if (innerBitmapMetadata != null) {
                        CaptureMetadata(innerBitmapMetadata, fullQuery);
                    }
                }
            }
        }

        private void InitRotationAndFlip(BitmapMetadata metadata) {

            if (metadata != null) {
                // from http://jpegclub.org/exif_orientation.html:
                //For convenience, here is what the letter F would look like if it were tagged correctly and displayed by a program that ignores the orientation tag (thus showing the stored image): 
                //
                //  1        2       3      4         5            6           7          8
                //
                //888888  888888      88  88      8888888888  88                  88  8888888888
                //88          88      88  88      88  88      88  88          88  88      88  88
                //8888      8888    8888  8888    88          8888888888  8888888888          88
                //88          88      88  88
                //88          88  888888  888888
                object value = metadata.GetQuery("/app1/ifd/{ushort=274}"); // 0x0112
                if (value != null) {
                    ushort s = (ushort)value;
                    // I'm not sure all cases can be represented properly w/ a single "flip" bool
                    switch (s) {
                        case 0:
                        case 1: fileRotation = 0; fileFlip = false; break;
                        case 2: fileRotation = 0; fileFlip = true; break;
                        case 3: fileRotation = 180; fileFlip = false; break;
                        case 4: fileRotation = 180; fileFlip = false; break;
                        case 5: fileRotation = -90; fileFlip = true; break; // ?
                        case 6: fileRotation = -90; fileFlip = false; break;
                        case 7: fileRotation = 0; fileFlip = true; break;
                        case 8: fileRotation = 90; fileFlip = false; break;
                        default: fileRotation = 0; fileFlip = false; break;
                    }
                }
            }
        }

        private Ratio GetRatioMetadata(BitmapMetadata metadata, string key)
        {
            if (metadata == null) {
                return Ratio.Invalid;
            }

            object value = metadata.GetQuery(key);
            ulong v;
            if (value is ulong)
                v = (ulong)value;
            else if (value is long)
                v = (ulong)(long) value;
            else if (value is ushort)
                v = (ulong)(ushort)value;
            else if (value == null)
                v = 0;
            else {
                Debug.Fail("missing case");
                v = 0;
            }
            int denominator = (int)(v >> 32);
            int numerator = (int)(v & 0xffffffff);
            Ratio ratio = new Ratio(numerator, denominator);
            return ratio;
        }

        // Designed to run on a background thread, but this class itself doesn't have any threading knowledge
        internal static class ImageDecoder
        {
            // displayWidth/Height is the maximum, the returned ImageInfo height/width will
            // be smaller in order to preserve aspect ratio.
            public static ImageInfo Decode(ImageOrigin file, int displayWidth, int displayHeight, ImageResolution resolution) {
                // useful for debugging:
                // System.Threading.Thread.Sleep(3000);

                if (resolution == ImageResolution.Thumbnail) {
                    return LoadImageThumbnail(file);
                } else if (displayHeight <= 225 && displayWidth <= 225) {
                    // hack-o-rama.  125 is the size of a thumbnail.
                    return LoadImageThumbnail(file);
                } else {

                    ImageInfo info = null;
                    //if (displayWidth > 0 && displayHeight > 0)
                    //{
                    //    info = LoadBitmapFast(file, displayWidth, displayHeight);
                    //    //info = LoadBitmapSmall(file, displayWidth, displayHeight);
                    //}
                    //else
                    //{
                    info = LoadImageSimple(file);

                    var size = info.SizePreservingAspectRatio(displayWidth, displayHeight);
                    var target = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Default);
                    var drawingVisual = new DrawingVisualWorkaround();
                    drawingVisual.BitmapScalingMode = BitmapScalingMode.HighQuality;
                    //RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.LowQuality);
                    DrawingContext drawingContext = drawingVisual.RenderOpen();
                    drawingContext.DrawImage(info.originalSource, new Rect(0, 0, size.Width, size.Height));
                    //drawingContext.DrawRectangle(Brushes.Azure, null, new Rect(50, 50, 350, 350));
                    drawingContext.Close();
                    target.Render(drawingVisual);
                    target.Freeze();
                    info.scaledSource = target;

                    if (resolution == ImageResolution.Print) {
                        // todo: don't create "target" in 1st place 
                        info.scaledSource = info.originalSource;
                    } else if (resolution != ImageResolution.Full) {
                        info.originalSource = null;
                    }

                    return info;
                }
            }

            private static ImageInfo LoadImageThumbnail(ImageOrigin file) {
                //System.Threading.Thread.Sleep(3000);
                BitmapDecoder decoder;
                try {
                    decoder = BitmapDecoder.Create(file.SourceUri,
                        BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
                } catch (OutOfMemoryException) {
                    // garbage collector hasn't run recently enough to catch up with native bitmaps
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                     return LoadImageThumbnail(file);
                    // UNDONE: detect infinite recursion case when we really are out of memory.
                    // Experimentally, we seem to bomb out when working set hits about 1.5gb on a 32bit box
                } catch (NotSupportedException) {
                    return ImageInfo.CreateInvalidImage(file);
                }
                    // seems like a WPF bug
                catch (OverflowException) {
                    return ImageInfo.CreateInvalidImage(file);
                }

                BitmapSource thumbnail = decoder.Frames[0].Thumbnail;
                if (thumbnail == null) {
                    return ImageInfo.CreateInvalidImage(file);
                    // happens with eg .png
                }
                // constructor for ImageInfo pulls interesting bits out of the metadata
                ImageInfo info = new ImageInfo(thumbnail, null, file);
                info.scaledSource = thumbnail;
                info.originalSource = null;
                info.InitRotationAndFlip(decoder.Frames[0].Metadata as BitmapMetadata);
                return info;
            }

            // Loads it slowly but has good working set, by setting DecodePixelWidth/Height before loading
            private static ImageInfo LoadImageSmall(ImageOrigin file, int displayWidth, int displayHeight)
            {
                Debug.Assert(displayHeight > 0, "this function shouldn't be used if display size not known");

                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.None;

                ImageInfo info = LoadImageSimple(file);
                if (info == null)
                    return null;

                // Setting DecodePixel* is a big savings in working set, and the whole point of this function
                Size decodeSize = info.SizePreservingAspectRatio(displayWidth, displayHeight);
                bi.DecodePixelHeight = (int)decodeSize.Height;
                bi.DecodePixelWidth = (int)decodeSize.Width;

                bi.UriSource = file.SourceUri;
                bi.EndInit();

                bi.Freeze();
                info.originalSource = bi;
                return info;
            }

            // Loads it pretty quick but uses extra memory due to WPF's bitmap caching
            private static ImageInfo LoadBitmapFast(ImageOrigin file, int displayWidth, int displayHeight)
            {
                Debug.Assert(displayHeight > 0, "this function shouldn't be used if display size not known");

                ImageInfo info = LoadImageSimple(file);
                if (info == null)
                    return null;

                BitmapSource frame = info.originalSource;
                Size decodeSize = info.SizePreservingAspectRatio(displayWidth, displayHeight);
                int height = (int)decodeSize.Height;
                int width = (int)decodeSize.Width;

                frame.Freeze();
                Transform transform = new ScaleTransform(
                    width / ((double)frame.PixelWidth),
                    height / ((double)frame.PixelHeight));
                transform.Freeze();
                TransformedBitmap b = new TransformedBitmap(frame, transform);

                b.Freeze();
                Debug.Assert(b.PixelHeight == height);
                Debug.Assert(b.PixelWidth == width);

                CachedBitmap cached = new CachedBitmap(b, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                // workaround -- CachedBitmap has a bug where it holds on to its original 
                // bitmap source.
                FieldInfo field = typeof(CachedBitmap).GetField("_source", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                field.SetValue(cached, null);

                cached.Freeze();
                Debug.Assert(cached.PixelHeight == height);
                Debug.Assert(cached.PixelWidth == width);

                info.originalSource = cached;

                return info;
            }

            private static BitmapDecoder LoadImageSimpleHelper(ImageOrigin file)
            {
                BitmapDecoder decoder;
                try {
                    decoder = BitmapDecoder.Create(file.SourceUri,
                        BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    // BitmapCacheOption.OnLoad is way slow if using the Small code path,
                    // but good for the Fast path.
                    // BitmapCacheOption.OnDemand is preferred for the Small path,
                    // might or might not be slightly slower than OnLoad for fast path.
                    // BitmapCacheOption.None doesn't seem to be good for either.
                } catch (NotSupportedException) {
                    return null;
                }
                    // seems like a WPF bug
                catch (OverflowException) {
                    return null;
                }
                return decoder;
            }

            private static ImageInfo LoadImageSimple(ImageOrigin file)
            {
                BitmapDecoder decoder;
                try
                {
                    decoder = LoadImageSimpleHelper(file);
                } catch (OutOfMemoryException) {
                    // garbage collector hasn't run recently enough to catch up with native bitmaps
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    decoder = LoadImageSimpleHelper(file);
                }
                if (decoder == null)
                    return ImageInfo.CreateInvalidImage(file);

                BitmapSource frame = decoder.Frames[0];
                BitmapMetadata metadata = frame.Metadata as BitmapMetadata;

                // constructor for ImageInfo pulls interesting bits out of the metadata
                ImageInfo info = new ImageInfo(frame, metadata, file);

                return info;
            }
        }
    }
}
