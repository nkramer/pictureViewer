/*
 * 
 * to do:
 * pixel-perfect mode taxes the CPU much more
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;
using Amib.Threading;
using System.Linq;
using Action = System.Action;
using pictureviewer; // dubious dependency
using Pictureviewer.Book;

namespace Pictureviewer.Core
{
    // Returned when an image is loaded.
    // TODO - Is this class actually used anymore?
    class LoadedEventArgs : EventArgs
    {
        public LoadedEventArgs(ImageInfo info, object requester)
        {
            this.ImageInfo = info;
            this.Requester = requester;
        }

        // The image that was loaded
        public readonly ImageInfo ImageInfo;
        
        // The object that requested the load in the first place, useful because 
        // the view that requested the load might no longer be the active view when the image is done loading.
        public readonly object Requester;
    }

    delegate void LoadedEventHandler(object sender, LoadedEventArgs args);

    // The LoaderMode determines the prefetch and caching policy.
    internal enum PrefetchPolicy {
        Slideshow, 
        PhotoGrid, 
        PageDesigner
    }

    // this class handles caching, calculating what the prefetch, and offloading work to background threads.
    // 
    internal class ImageLoader
    {
        // Debug.Fail if the condition is false
        private static void Assert(bool condition, string message = "") {
            if (!condition) {
                Debug.Fail(message);
            }
        }

        private enum PrefetchRequestState {
            Pending, // Hasn't started loading yet
            InProgress, // Has started downloading/loading
            Done // Fully loaded & decoded
        }

        // One image in the image loader's prefetch list & cache. The cache entry needs to remember
        // what resolution the image was intended to be decoded for, because an
        // image may be loaded multiple times at different resolutions. 
        private class PrefetchRequest : ICloneable {
            // Width and height are the desired # pixels for the ImageInfo.originalSource will have when loaded.
            // (Ignored if scalingBehavior == thumbnail)
            public PrefetchRequest(ImageOrigin origin, int width, int height, ScalingBehavior scalingBehavior) {
                this.origin = origin;
                this.height = height;
                this.width = width;
                this.scalingBehavior = scalingBehavior;
            }

            public readonly int width; // in pixels
            public readonly int height; // in pixels

            // Desired downsampling policy to decode the image to
            public readonly ScalingBehavior scalingBehavior;

            // The image to load -- basically, the image ID
            public readonly ImageOrigin origin;

            // The Image that's been loaded, or null if the image hasn't been loaded yet.
            public ImageInfo info = null;

            // Parties we need to notify when the image is ready.
            public List<Action<ImageInfo>> CompletedCallbacks = new List<Action<ImageInfo>>();
            
            // The thread pool work item for loading this image
            public IWorkItemResult workitem;
            
            // Current status of the image load -- Pending, InProgress, Done
            public PrefetchRequestState state = PrefetchRequestState.Pending;

            // Assert that the object is internally consistent
            public void AssertInvariant() {
                Assert(origin != null);
                if (CompletedCallbacks.Count > 0) {
                    Assert(info == null);
                    // better not have people waiting if you already know the answer
                }
                if (info != null) {
                    Assert(state == PrefetchRequestState.Done);
                    // there is a brief timing window where workitem.IsCompleted but the info & State properties haven't been updated yet
                }
                if (state == PrefetchRequestState.Done)
                    Assert (info != null);
            }

            // Cache entries are equal if they point to the same image origin, have the same desire scaling, and the same desired width/height.
            public override bool Equals(object obj) {
                if (obj is PrefetchRequest) {
                    var request = obj as PrefetchRequest;
                    return this.origin == request.origin && this.width == request.width && this.height == request.height && this.scalingBehavior == request.scalingBehavior;
                } else
                    return false;
            }
            public override int GetHashCode() {
                return this.origin.SourcePath.GetHashCode(); // UNDONE: isn't the greatest hash function in the world but it's correct...
            }

            public override string ToString() {
                return origin.DisplayName + " " + state + " " + width+"x"+height+" " + scalingBehavior;
            }

            // Create a new and she with the same origin/width/height/scaling,
            // and set all the other fields to their default values.
            public object Clone() {
                PrefetchRequest request = (PrefetchRequest)this.MemberwiseClone();
                request.CompletedCallbacks = new List<Action<ImageInfo>>();
                request.workitem = null;
                request.state = PrefetchRequestState.Pending;
                return request;
            }
        }

        private PrefetchPolicy prefetchPolicy = PrefetchPolicy.Slideshow;

        public PrefetchPolicy PrefetchPolicy
        {
            get { return prefetchPolicy; }
            set { prefetchPolicy = value; UpdateWorkItems(); }
        }

        // The caller of the image loader will need to set these two properties appropriately
        public int ThumbnailsPerPage = -1; // approximate -- doesn't need to be 100% accurate.
        public ImageOrigin FirstThumbnail = null; // First visible thumbnail
        
        // In slideshow mode, how many images after the current image to prefetch
        private readonly int Lookahead = 3;
        
        // In slideshow mode, how many images before the current image to prefetch
        private readonly int Lookbehind = 2;

        // All image origins in the display set
        private ImageOrigin[] imageOrigins = new ImageOrigin[0];
        
        // The Dispatcher for the UI thread
        private Dispatcher mainDispatcher;

        // The currently displayed image in a slideshow, or focused image in thumbnail mode
        private ImageOrigin focusedImage = null;
        
        // The list of prefetch requests the loader has calculated
        private List<PrefetchRequest> cache = new List<PrefetchRequest>();

        // Map Image origins to their corresponding prefetch request.
        // Possibly a premature optimization for form startup time.
        private ILookup<ImageOrigin, PrefetchRequest> cacheLookup;
        private List<PrefetchRequest> unpredictedRequests = new List<PrefetchRequest>(); // Requests that weren't anticipated by the prefetcher
        
        // Thread pool for image decoding that doesn't block the UI thread
        private SmartThreadPool smartThreadPool = new SmartThreadPool();
        
        // Size in physical pixels the image will be displayed at
        private int clientHeight;
        private int clientWidth;

        // Assert that the object is internally consistent
        private void AssertInvariant() {
            Assert(imageOrigins != null);
            Assert(mainDispatcher != null);
            // next line is rather slow
            //Assert(focusedImage == null || imageOrigins.Contains(focusedImage));
            Assert(cache != null);
            //Assert(cache.Count <= imageOrigins.Count());

            foreach (var request in cache) {
                request.AssertInvariant();
            }
        }

        // Should never create more than one per UI thread, the 
        // CPU usage and memory usage would get out of hand
        public ImageLoader() {
            this.smartThreadPool.MaxThreads = Environment.ProcessorCount;
            this.mainDispatcher = Dispatcher.CurrentDispatcher;
            AssertInvariant();
        }

        // Size in physical pixels the image will be displayed at
        public void SetTargetSize(int width, int height) {
            this.clientWidth = width;
            this.clientHeight = height;
            UpdateWorkItems();
        }

        // Establishes the set of images in the display set, and which one currently has focus/is being displayed
        public void SetImageOrigins(ImageOrigin[] imageOrigins, ImageOrigin focusedImage) {
            Assert(focusedImage == null || imageOrigins.Contains(focusedImage));
            if (focusedImage == null && imageOrigins.Length > 0)
                focusedImage = imageOrigins[0];

            this.focusedImage = focusedImage;
            this.imageOrigins = imageOrigins;
            UpdateWorkItems();
        }

        // Tells the loader which image of the display set is currently displayed/focused
        public void SetFocus(ImageOrigin focusedImage) {
            Assert(focusedImage == null || imageOrigins.Contains(focusedImage));
            if (focusedImage == null && imageOrigins.Length > 0)
                focusedImage = imageOrigins[0];

            this.focusedImage = focusedImage;
            UpdateWorkItems();
        }

        private IEnumerable<PrefetchRequest> CachesForPage(PhotoPageModel page, int width, int height, ScalingBehavior scalingBehavior) {
            var res = page.Images.Select(i => new PrefetchRequest(i, width, height, scalingBehavior));
            return res;
        }

        // HACK: shouldn't be public
        public void UpdateWorkItems() {
            AssertInvariant();
            ImageOrigin focus = focusedImage;
            var desiredCache = new List<PrefetchRequest>();

            // UNDONE:
            // create deep copy of unpredictedRequests so desiredCache doesn't contain
            // any items that are request.state != CacheEntryState.Pending, confusing
            // the eventual merge
            var unpredictedCopy = unpredictedRequests;//.Select(ce => (CacheEntry) ce.Clone());
            //var unpredictedCopy = unpredictedRequests.Select(ce => (CacheEntry) ce.Clone());
            desiredCache.AddRange(unpredictedCopy);

            // in priority order
            if (this.PrefetchPolicy == PrefetchPolicy.Slideshow) {
                int focusIndex = ImageOrigin.GetIndex(imageOrigins, focus);
                // Full-screen image being displayed
                if (focus != null) {
                    desiredCache.Add(new PrefetchRequest(focus, clientWidth, clientHeight, ScalingBehavior.Full));
                }

                // prefetch of next full-screen images
                for (int i = 1; i <= Lookahead; i++) {
                    desiredCache.Add(new PrefetchRequest(ImageOrigin.NextImage(imageOrigins, focusIndex, +i), clientWidth, clientHeight, ScalingBehavior.Full));
                }

                // previous full-screen images
                for (int i = 1; i <= Lookbehind; i++) {
                    desiredCache.Add(new PrefetchRequest(ImageOrigin.NextImage(imageOrigins, focusIndex, -i), clientWidth, clientHeight, ScalingBehavior.Full));
                }
            } else if (this.PrefetchPolicy == PrefetchPolicy.PhotoGrid) {
                PhotoGridCachePolicy(desiredCache);
            } else if (this.PrefetchPolicy == PrefetchPolicy.PageDesigner) {
                BookModel book = RootControl.Instance.book;
                PhotoPageModel page = book.SelectedPage;
                
                if (page != null) {
                    desiredCache.AddRange(CachesForPage(page, clientWidth, clientHeight, ScalingBehavior.Small));

                    // next + prev page
                    int pageNum = book.Pages.IndexOf(page);
                    if (pageNum < book.Pages.Count - 1)
                        desiredCache.AddRange(CachesForPage(book.Pages[pageNum + 1], clientWidth, clientHeight, ScalingBehavior.Small));
                    if (pageNum > 0)
                        desiredCache.AddRange(CachesForPage(book.Pages[pageNum - 1], clientWidth, clientHeight, ScalingBehavior.Small));
                }

                foreach (var p in book.Pages) {
                    desiredCache.AddRange(CachesForPage(p, 125, 125, ScalingBehavior.Small));
                }

                PhotoGridCachePolicy(desiredCache);
            } else {
                Debug.Fail("What other kind of loader mode is there?");
            }

            desiredCache = desiredCache.Where(c => c.origin != null).Distinct().ToList(); // in case the imageOrigins collection is small and lookahead wraps around and catches lookbehind

            var newCache = new List<PrefetchRequest>();
            // Update any existing entries that correspond to the things we want.
            // If the new thing isn't in the existing cache, add it.
            // If it is in the existing cache, cancel any associated work items and requeue them to reflect our new priorities
            foreach (var request in desiredCache) {
                PrefetchRequest existing = cache.Find((x) => x.Equals(request));
                PrefetchRequest newRequest = null;
                if (existing == null) {
                    newRequest = request;
                    QueueWorkItem(request);
                } else if (existing.state == PrefetchRequestState.Done || existing.state == PrefetchRequestState.InProgress) {
                    newRequest = existing;
                } else {
                    // Delete the existing work item & create a new one so we can use updated priorities
                    Assert(existing.state == PrefetchRequestState.Pending);
                    newRequest = request;
                    newRequest.CompletedCallbacks = existing.CompletedCallbacks;
                    // BUG: race condition: the request starts running now. Thread pool would finish the work item like it should, however
                    // we'll end up with multiple cache entries and will load the image twice
                    existing.workitem.Cancel();
                    QueueWorkItem(request);

                    if (existing != null)
                        Debug.Assert(existing.CompletedCallbacks.Count == newRequest.CompletedCallbacks.Count);
                }

                if (existing != null)
                    Debug.Assert(existing.CompletedCallbacks.Count == newRequest.CompletedCallbacks.Count);

                // UNDONE: can be more clever about lower resolution requests when you already have a higher resolution
                Assert(newRequest != null);
                newCache.Add(newRequest);
            }

            // Now cancel any remaining work items (ie, everything we didn't touch above)
            foreach (var request in this.cache) {
                PrefetchRequest desired = desiredCache.Find((x) => x.Equals(request));
                if (desired == null) {
                    request.workitem.Cancel();
                }
            }

            this.cache = newCache;
            cacheLookup = cache.ToLookup((x) => x.origin);
            AssertInvariant();
        }

        private void PhotoGridCachePolicy(List<PrefetchRequest> desiredCache) {
            int firstIndex = ImageOrigin.GetIndex(imageOrigins, FirstThumbnail);

            // thumbnails currently displayed + one more page
            for (int i = 0; i <= ThumbnailsPerPage * 2; i++) {
                desiredCache.Add(new PrefetchRequest(ImageOrigin.NextImage(imageOrigins, firstIndex, +i), 
                    125, 125, ScalingBehavior.Thumbnail));
            }

            // thumbnails for previous page
            for (int i = 0; i <= ThumbnailsPerPage; i++) {
                desiredCache.Add(new PrefetchRequest(ImageOrigin.NextImage(imageOrigins, firstIndex, -i), 
                    125, 125, ScalingBehavior.Thumbnail));
            }

            // larger images for grid currently displayed
            //for (int i = 0; i <= ThumbnailCount; i++) {
            //    //desiredCache.Add(new CacheEntry(imageOrigins[SlideShow.NextIndex(imageOrigins, origin.Index, +i)], 125, 125, ScalingBehavior.Small));//new Size(clientWidth, clientHeight)));
            //}

            // Full-screen image being displayed -- Really we just want to not evict whatever's there in the cache,
            // ideally we wouldn't actively load it
            //if (focus != null)
            //{
            //    desiredCache.Add(new CacheEntry(focus, clientWidth, clientHeight, ScalingBehavior.Full));
            //}
        }

        private void QueueWorkItem(PrefetchRequest request) {
            // bug: uncomment Assert when race condition fixed
            Assert(request.state == PrefetchRequestState.Pending);
            request.workitem = smartThreadPool.QueueWorkItem(
                new WorkItemCallback((object ignored) => { BackgroundBeginLoad(request); return null; }),
                WorkItemPriority.BelowNormal);
        }

        public void ClearCache() {
            // UNDONE
        }

        public void Shutdown() {
            // UNDONE
        }

        public ImageInfo LoadSync(ImageOrigin origin, int width, int height, ScalingBehavior scalingBehavior) {
            // ignores cache
            ImageInfo info = ImageInfo.Load(origin, width, height, scalingBehavior);
            return info;
        }

        // not used at the moment
        public void BeginLoadUnpredicted(ImageOrigin origin, int width, int height, ScalingBehavior scalingBehavior, Action<ImageInfo> completed)
        {
            //Debug.WriteLine("" + width + " " + height);
            BeginLoadInternal(origin, width, height, scalingBehavior, completed, true);
        }

        public void BeginLoad(ImageOrigin origin, int width, int height, ScalingBehavior scalingBehavior, Action<ImageInfo> completed)
        {
            BeginLoadInternal(origin, width, height, scalingBehavior, completed, false);
        }

        private void BeginLoadInternal(ImageOrigin origin, int width, int height, ScalingBehavior scalingBehavior, Action<ImageInfo> completed, bool unpredicted)
        {
            AssertInvariant();
            Debug.Assert(completed != null);
            IEnumerable<PrefetchRequest> entries = cacheLookup[origin].Where((x) =>
                x.origin.Equals(origin) && (x.height >= height || x.width >= width) && x.scalingBehavior == scalingBehavior
                );

            if (!unpredicted && entries.Count() == 0) {
                // hack: we're here because we gave 'em a thumbnail when they asked for a small
                // retry w/o height/width requirement
                entries = cacheLookup[origin].Where((x) =>
                    x.origin.Equals(origin) && x.scalingBehavior == scalingBehavior
                );
                if (entries.Count() == 0)
                    throw new Exception("Request for unexpected image; loader mode must be wrong");
            }

            PrefetchRequest request;
            if (entries.Count() > 0) {
                request = entries.First();
            } else { // create request
                Debug.Assert(unpredicted);
                request = new PrefetchRequest(origin, width, height, scalingBehavior);
                unpredictedRequests.Add(request);
                UpdateWorkItems();
            }

            request.AssertInvariant();
            if (request.info != null) {
                // Invokes the callback asynchronously to avoid changing the event order
                // in the case the item is in the cache
                mainDispatcher.BeginInvoke(
                    new System.Action(() => {
                        RaiseLoaded(completed, request);
                    }));
            } else {
                request.CompletedCallbacks.Add(completed);
                // no race condition, when the threadpool completes, it will call the UI thread to 
                // clean up the Requesters list
            }
            AssertInvariant();
        }

        private void RaiseLoaded(Action<ImageInfo> completed, PrefetchRequest request)
        {
            if (unpredictedRequests.Contains(request))
                unpredictedRequests.Remove(request);
            completed(request.info);
        }

        public bool IsTriageMode; // whether to update selection based on file existence

        // for debugging only
        //public event LoadedEventHandler PreloadComplete;

        // Because new requests can supersede old ones,
        // we don't process the request right away, rather we
        // delay processing until the message queue has been idle for a few 
        // moments (meaning new requests have stopped coming in)
        private void BackgroundBeginLoad(PrefetchRequest request) {
            request.state = PrefetchRequestState.InProgress;
            ImageInfo info = ImageInfo.Load(request.origin,
                request.width,
                request.height,
                request.scalingBehavior);
            //Debug.Assert(info.scaledSource != null);
            
            // send answer back to UI thread
            var callback = new LoadCompletedCallback(this.LoadCompletedPart1);
            mainDispatcher.BeginInvoke(callback, DispatcherPriority.Background, request, info);
        }

        // Trying to finesse queue prioritization.
        // If you set the priority high, and there's a whole lot of thumbnails that load fast enough,
        // you get into a situation where it never renders because just as it's finishing up laying out, another thumbnail 
        // comes along and invalidates everything.  On the other hand, if you naively put the priority low, you'll run 
        // layout once for every thumbnail no matter how fast they come in.
        // Here we attempt to batch them up, but once we start a layout we try to always render it.
        private List<LoadedPartiallyCompleted> pendingLoadedEvents = new List<LoadedPartiallyCompleted>();

        private class LoadedPartiallyCompleted {
            public PrefetchRequest request;
            public ImageInfo info;
        }

        private delegate void LoadCompletedCallback(PrefetchRequest request, ImageInfo info);

        private void LoadCompletedPart1(PrefetchRequest request, ImageInfo info) {
            pendingLoadedEvents.Add(new LoadedPartiallyCompleted() { request = request, info = info });
            var callback = new Action(this.LoadCompletedPart2);
            mainDispatcher.BeginInvoke(callback, DispatcherPriority.Normal);
        }

        private void LoadCompletedPart2()
        {
            foreach (var partial in pendingLoadedEvents) {
                PrefetchRequest request = partial.request;
                ImageInfo info = partial.info;
                AssertInvariant();
                request.info = info;
                request.state = PrefetchRequestState.Done;
                foreach (var completedCallback in request.CompletedCallbacks) {
                    RaiseLoaded(completedCallback, request);
                }
                request.CompletedCallbacks.Clear();
                request.AssertInvariant();
                AssertInvariant();
            }
            pendingLoadedEvents.Clear();
        }
    }
}