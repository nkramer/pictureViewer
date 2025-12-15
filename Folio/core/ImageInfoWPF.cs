#nullable disable
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Folio.Core;
// The parts of ImageInfo that can only be implemented in WPF (not Silverlight).
public partial class ImageInfo {
    public static ImageInfo Load(LoadRequest request) {
        return ImageDecoder.Decode(request);
    }

    // Makes the protected VisualBitmapScalingMode property into a public property
    internal class DrawingVisualWorkaround : DrawingVisual {
        public BitmapScalingMode BitmapScalingMode {
            get { return VisualBitmapScalingMode; }
            set { VisualBitmapScalingMode = value; }
        }
    }

    private ImageInfo(BitmapSource bitmapSource, BitmapMetadata metadata, ImageOrigin origin) {
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
        // and https://nicholasarmstrong.com/2010/02/exif-quick-reference/
        focalLength = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=37386}");    // ratio
        isospeed = GetIntMetadata(metadata, "/app1/ifd/exif/{ushort=34855}");         // int
        exposureTime = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=33434}");   // ratio
        //whiteBalance = GetIntMetadata(metadata, "/app1/ifd/exif/{ushort=41987}");   // int/bool
        fstop = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=33437}");          // ratio
        exposureBias = GetRatioMetadata(metadata, "/app1/ifd/exif/{ushort=37380}");   // ratio

        InitRotationAndFlip(metadata);

        if (metadata != null) {
            object v = metadata.GetQuery("/app1/ifd/{ushort=271}");                   // Manufacturer, string 
            object v2 = metadata.GetQuery("/app1/ifd/{ushort=272}");                  // Model, string 
            object v3 = metadata.GetQuery("/xmp/MicrosoftPhoto:LensModel");
            object v4 = metadata.GetQuery("/app1/ifd/exif/{ushort=42036}");           // LensModel?, string 
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

    private int GetIntMetadata(BitmapMetadata metadata, string key) {
        if (metadata == null) {
            return 0;
        }

        object value = metadata.GetQuery(key);
        ulong v;
        if (value is ulong)
            v = (ulong)value;
        else if (value is long)
            v = (ulong)(long)value;
        else if (value is ushort)
            v = (ulong)(ushort)value;
        else if (value == null)
            v = 0;
        else {
            Debug.Fail("missing case");
            v = 0;
        }

        return (int)v;
    }

    private Ratio GetRatioMetadata(BitmapMetadata metadata, string key) {
        if (metadata == null) {
            return Ratio.Invalid;
        }

        object value = metadata.GetQuery(key);
        ulong v;
        if (value is ulong)
            v = (ulong)value;
        else if (value is long)
            v = (ulong)(long)value;
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
        if (denominator == 0)
            denominator = 1;   // Otherwise get lots of images with invalid metadata, especially with iOS HEIC.
        Ratio ratio = new Ratio(numerator, denominator);
        return ratio;
    }

    // Designed to run on a background thread, but this class itself doesn't have any threading knowledge
    internal static class ImageDecoder {
        // displayWidth/Height is the maximum, the returned ImageInfo height/width will
        // be smaller in order to preserve aspect ratio.
        public static ImageInfo Decode(LoadRequest request) {
            // useful for debugging:
            // System.Threading.Thread.Sleep(3000);

            var stopwatch = Stopwatch.StartNew();
            DateTime startTime = DateTime.Now;
            long memoryBefore = GC.GetTotalMemory(false);

            ImageInfo info = null;

            if (request.scalingBehavior == ScalingBehavior.Thumbnail) {
                info = LoadImageThumbnail(request.origin);
            } else if (request.height <= 225 && request.width <= 225) {
                // hack-o-rama.  125 is the size of a thumbnail.
                info = LoadImageThumbnail(request.origin);
            } else {

                //if (displayWidth > 0 && displayHeight > 0)
                //{
                //    info = LoadBitmapFast(file, displayWidth, displayHeight);
                //    //info = LoadBitmapSmall(file, displayWidth, displayHeight);
                //}
                //else
                //{
                info = LoadImageSimple(request.origin);

                var size = info.SizePreservingAspectRatio(request.width, request.height);
                var target = RetryIfOutOfMemory(() => new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Default));
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

                if (request.scalingBehavior == ScalingBehavior.Print) {
                    // todo: don't create "target" in 1st place
                    info.scaledSource = info.originalSource;
                } else if (request.scalingBehavior != ScalingBehavior.Full) {
                    info.originalSource = null;
                }
            }

            stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);

            if (info != null) {
                long totalPixels = (long)info.pixelWidth * info.pixelHeight;
                // double memoryUsedMB = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);
                double totalMemoryMB = memoryAfter / (1024.0 * 1024.0);

                Log.Information("ImagePath: {ImagePath} | Duration: {DurationMs}ms | Pixels: {Pixels} ({Width}x{Height}) | TotalMemory: {TotalMemoryMB:F2}MB | Type: {Type}",
                    info.origin.DisplayName,
                    stopwatch.ElapsedMilliseconds,
                    totalPixels,
                    info.pixelWidth,
                    info.pixelHeight,
                    totalMemoryMB,
                    request.scalingBehavior);
            }

            return info;
        }

        private static BitmapSource CopyBitmap(BitmapSource source) {
            // from https://stackoverflow.com/questions/4118658/copy-bitmap-into-other-bitmap-with-wpf

            int stride = source.PixelWidth * (source.Format.BitsPerPixel / 8);
            byte[] data = new byte[stride * source.PixelHeight];
            source.CopyPixels(data, stride, 0);

            WriteableBitmap target = new WriteableBitmap(
                  source.PixelWidth,
                  source.PixelHeight,
                  source.DpiX, source.DpiY,
                  source.Format, null);

            target.WritePixels(
                  new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight),
                  data, stride, 0);
            target.Freeze();  // make it cross-thread accessible
            return target;
        }

        // for ExtractShellThumbnail to call
        #region Windows Shell API P/Invoke declarations

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem {
            void BindToHandler(IntPtr pbc,
                [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
                [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                out IntPtr ppv);

            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory {
            [PreserveSig]
            int GetImage(
                [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
                [In] SIIGBF flags,
                [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE {
            public int cx;
            public int cy;

            public SIZE(int cx, int cy) {
                this.cx = cx;
                this.cy = cy;
            }
        }

        [Flags]
        private enum SIIGBF {
            SIIGBF_RESIZETOFIT = 0x00,
            SIIGBF_BIGGERSIZEOK = 0x01,
            SIIGBF_MEMORYONLY = 0x02,
            SIIGBF_ICONONLY = 0x04,
            SIIGBF_THUMBNAILONLY = 0x08,
            SIIGBF_INCACHEONLY = 0x10,
        }

        private enum SIGDN : uint {
            NORMALDISPLAY = 0,
            PARENTRELATIVEPARSING = 0x80018001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000,
            PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
            PARENTRELATIVE = 0x80080001
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        #endregion


        // Helper method to extract thumbnail using Windows Shell API
        private static BitmapSource ExtractShellThumbnail(string filePath, uint thumbnailSize = 256) {
            IShellItem shellItem = null;
            IntPtr hbitmap = IntPtr.Zero;

            try {
                // Create IShellItem from file path
                Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
                int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, shellItemGuid, out shellItem);

                if (hr != 0) {
                    Debug.WriteLine($"SHCreateItemFromParsingName failed for {filePath}: HRESULT = 0x{hr:X}");
                    return null;
                }

                // Cast to IShellItemImageFactory (IShellItem implements this interface)
                IShellItemImageFactory imageFactory = (IShellItemImageFactory)shellItem;

                // Request thumbnail with THUMBNAILONLY flag to get embedded/cached thumbnails
                SIZE size = new SIZE((int)thumbnailSize, (int)thumbnailSize);
                SIIGBF flags = SIIGBF.SIIGBF_THUMBNAILONLY | SIIGBF.SIIGBF_INCACHEONLY;

                hr = imageFactory.GetImage(size, flags, out hbitmap);

                // If thumbnail-only failed, try with a more permissive flag
                if (hr != 0 || hbitmap == IntPtr.Zero) {
                    Debug.WriteLine($"GetImage with THUMBNAILONLY failed (0x{hr:X}), trying RESIZETOFIT for {filePath}");
                    flags = SIIGBF.SIIGBF_RESIZETOFIT;
                    hr = imageFactory.GetImage(size, flags, out hbitmap);
                }

                if (hr != 0 || hbitmap == IntPtr.Zero) {
                    Debug.WriteLine($"GetImage failed for {filePath}: HRESULT = 0x{hr:X}");
                    return null;
                }

                // Convert HBITMAP to BitmapSource
                BitmapSource bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hbitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bitmap.Freeze();
                return bitmap;

            } catch (COMException comEx) {
                Debug.WriteLine($"Shell thumbnail extraction failed for {filePath}: COM Error 0x{comEx.HResult:X} - {comEx.Message}");
                return null;
            } catch (Exception ex) {
                Debug.WriteLine($"Shell thumbnail extraction failed for {filePath}: {ex.Message}");
                return null;
            } finally {
                // Clean up resources
                if (hbitmap != IntPtr.Zero) {
                    DeleteObject(hbitmap);
                }

                if (shellItem != null) {
                    Marshal.ReleaseComObject(shellItem);
                }
            }
        }

        private static ImageInfo LoadImageThumbnail(ImageOrigin file) {
            BitmapDecoder decoder;
            try {
                //decoder = RetryIfOutOfMemory(() => BitmapDecoder.Create(file.SourceUri,
                //    BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand));
                decoder = RetryIfOutOfMemory(() => BitmapDecoder.Create(file.SourceUri, BitmapCreateOptions.None, BitmapCacheOption.None));
            } catch (NotSupportedException) {
                Debug.WriteLine($"fail: {file.DisplayName}");
                return ImageInfo.CreateInvalidImage(file);
            } catch (OverflowException) {
                Debug.WriteLine($"fail: {file.DisplayName}");
                // OverflowException seems like a WPF bug
                return ImageInfo.CreateInvalidImage(file);
            }

            BitmapSource thumbnail = decoder.Frames[0].Thumbnail;

            // If WPF couldn't find a thumbnail, try Windows Shell API as fallback
            if (thumbnail == null) {
                Debug.WriteLine($"WPF thumbnail not found, trying Shell API: {file.DisplayName}");
                thumbnail = ExtractShellThumbnail(file.SourcePath);

                if (thumbnail == null) {
                    Debug.WriteLine($"invalid: {file.DisplayName}");
                    return ImageInfo.CreateInvalidImage(file);
                    // happens with eg .png
                }
            }

            // constructor for ImageInfo pulls interesting bits out of the metadata
            ImageInfo info = new ImageInfo(thumbnail, null, file);

            // HEIC files require copying the thumbnail, otherwise the scaledSource will later show up as size 1x1. Threading issue in WIC?
            info.scaledSource = CopyBitmap(thumbnail);
            Debug.Assert(thumbnail.PixelHeight > 10);
            info.originalSource = null;
            info.InitRotationAndFlip(decoder.Frames[0].Metadata as BitmapMetadata);
            return info;
        }

        //// Loads it slowly but has good working set, by setting DecodePixelWidth/Height before loading
        //private static ImageInfo LoadImageSmall(ImageOrigin file, int displayWidth, int displayHeight)
        //{
        //    Debug.Assert(displayHeight > 0, "this function shouldn't be used if display size not known");

        //    BitmapImage bi = new BitmapImage();
        //    bi.BeginInit();
        //    bi.CacheOption = BitmapCacheOption.OnLoad;
        //    bi.CreateOptions = BitmapCreateOptions.None;

        //    ImageInfo info = LoadImageSimple(file);
        //    if (info == null)
        //        return null;

        //    // Setting DecodePixel* is a big savings in working set, and the whole point of this function
        //    Size decodeSize = info.SizePreservingAspectRatio(displayWidth, displayHeight);
        //    bi.DecodePixelHeight = (int)decodeSize.Height;
        //    bi.DecodePixelWidth = (int)decodeSize.Width;

        //    bi.UriSource = file.SourceUri;
        //    bi.EndInit();

        //    bi.Freeze();
        //    info.originalSource = bi;
        //    return info;
        //}

        //// Loads it pretty quick but uses extra memory due to WPF's bitmap caching
        //private static ImageInfo LoadBitmapFast(ImageOrigin file, int displayWidth, int displayHeight)
        //{
        //    Debug.Assert(displayHeight > 0, "this function shouldn't be used if display size not known");

        //    ImageInfo info = LoadImageSimple(file);
        //    if (info == null)
        //        return null;

        //    BitmapSource frame = info.originalSource;
        //    Size decodeSize = info.SizePreservingAspectRatio(displayWidth, displayHeight);
        //    int height = (int)decodeSize.Height;
        //    int width = (int)decodeSize.Width;

        //    frame.Freeze();
        //    Transform transform = new ScaleTransform(
        //        width / ((double)frame.PixelWidth),
        //        height / ((double)frame.PixelHeight));
        //    transform.Freeze();
        //    TransformedBitmap b = new TransformedBitmap(frame, transform);

        //    b.Freeze();
        //    Debug.Assert(b.PixelHeight == height);
        //    Debug.Assert(b.PixelWidth == width);

        //    CachedBitmap cached = new CachedBitmap(b, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

        //    // workaround -- CachedBitmap has a bug where it holds on to its original 
        //    // bitmap source.
        //    FieldInfo field = typeof(CachedBitmap).GetField("_source", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        //    field.SetValue(cached, null);

        //    cached.Freeze();
        //    Debug.Assert(cached.PixelHeight == height);
        //    Debug.Assert(cached.PixelWidth == width);

        //    info.originalSource = cached;

        //    return info;
        //}

        private static BitmapDecoder LoadImageSimpleHelper(ImageOrigin file) {
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

        private static ImageInfo LoadImageSimple(ImageOrigin file) {
            BitmapDecoder decoder = RetryIfOutOfMemory(() => LoadImageSimpleHelper(file));

            if (decoder == null)
                return ImageInfo.CreateInvalidImage(file);

            BitmapSource frame = decoder.Frames[0];
            BitmapMetadata metadata = frame.Metadata as BitmapMetadata;

            // constructor for ImageInfo pulls interesting bits out of the metadata
            ImageInfo info = new ImageInfo(frame, metadata, file);

            return info;
        }

        // Ideally we wouldn't run out of memory because we would deterministically 
        // dispose of images ahead of time, but WPF doesn't make that easy
        private static T RetryIfOutOfMemory<T>(Func<T> f) {
            try {
                return f();
            } catch (OutOfMemoryException) {
                // garbage collector hasn't run recently enough to catch up with native bitmaps
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return f();
            }
        }
    }
}
