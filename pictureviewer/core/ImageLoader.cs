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
using System.Threading;
using System.Runtime.CompilerServices;

namespace Pictureviewer.Core
{
    // Captures the key parameters for loading an image -- i.e., which image, and how big.
    public class LoadRequest
    {
        // Width and height are the desired # pixels for the ImageInfo.originalSource will have when loaded.
        // (Ignored if scalingBehavior == thumbnail)
        public LoadRequest(ImageOrigin origin, int width, int height, ScalingBehavior scalingBehavior)
        {
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

        // LoadRequests are equal if they point to the same image origin, have the same desired scaling, and the same desired width/height.
        public override bool Equals(object obj)
        {
            if (obj is LoadRequest)
            {
                var request = obj as LoadRequest;
                return this.origin == request.origin && this.width == request.width && this.height == request.height && this.scalingBehavior == request.scalingBehavior;
            }
            else
                return false;
        }
        public override int GetHashCode()
        {
            return this.origin.SourcePath.GetHashCode(); // UNDONE: isn't the greatest hash function in the world but it's correct...
        }

        public override string ToString()
        {
            return origin.DisplayName + " " + width + "x" + height + " " + scalingBehavior;
        }
    }

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
    internal enum PrefetchPolicy : int {
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

        // Where this request is in the loading process.
        // Allowed transitions: 
        // Pending -> InProgress  -> Done, 
        // or Pending -> Aborted
        private enum CacheEntryState {
            Pending, // Hasn't started loading yet
            InProgress, // Has started downloading/loading
            Done, // Fully loaded & decoded
            Aborted // Request rescinded before loading began
        }

        // Represents images that have been loaded, and images that we want to load.
        // One image in the image loader's prefetch list & cache. The cache entry needs to remember
        // what resolution the image was intended to be decoded for, because an
        // image may be loaded multiple times at different resolutions. 
        private class CacheEntry
        {
            // The image that's cached or requested to be loaded
            public readonly LoadRequest request;

            public CacheEntry(LoadRequest request)
            {
                this.request = request;
            }

            // The Image that's been loaded, or null if the image hasn't been loaded yet.
            public ImageInfo info = null;

            // Parties we need to notify when the image is ready.
            public List<Action<ImageInfo>> CompletedCallbacks = new List<Action<ImageInfo>>();

            // The thread pool work item for loading this image
            public IWorkItemResult workitem;

            // Current status of the image load -- Pending, InProgress, Done
            public CacheEntryState state = CacheEntryState.Pending;

            // Assert that the object is internally consistent
            public void AssertInvariant()
            {
                Assert(request.origin != null);
                if (CompletedCallbacks.Count > 0)
                {
                    Assert(info == null);
                    // better not have people waiting if you already know the answer
                }
                if (info != null)
                {
                    Assert(state == CacheEntryState.Done);
                    // there is a brief timing window where workitem.IsCompleted but the info & State properties haven't been updated yet
                }
                if (state == CacheEntryState.Done)
                    Assert(info != null);
            }
        }

        // The current policy for which images to prefetch and cache
        private PrefetchPolicy prefetchPolicy = PrefetchPolicy.Slideshow;

        // The current policy for which images to prefetch and cache
        public PrefetchPolicy PrefetchPolicy
        {
            get { return prefetchPolicy; }
            set { prefetchPolicy = value; UpdateWorkItems(); }
        }

        // The caller of the image loader will need to set these two properties appropriately.
        // Used with PrefetchPolicy.PhotoGrid & PageDesigner, ignored for Slideshow policy.
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
        
        // The list of cache entries the loader has calculated to prefetch & cache
        private List<CacheEntry> cache = new List<CacheEntry>();

        // Map Image origins to their corresponding cache entry.
        // Possibly a premature optimization for form startup time.
        private ILookup<ImageOrigin, CacheEntry> cacheLookup;
        private List<CacheEntry> unpredictedRequests = new List<CacheEntry>(); // Requests that weren't anticipated by the prefetcher
        
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

            foreach (var entry in cache) {
                entry.AssertInvariant();
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

        // Begin prefetching all the images the prefetch policy calls for.
        // We calculate a new set of ImageCacheEntries, and create thread 
        // pool work items to load the ones that haven't been loaded.
        // If this isn't the first time we've run UpdateWorkItems, 
        // there may already be thread pool work items -- we update them 
        // to reflect the new batch of cache entries.
        // HACK: shouldn't be public
        public void UpdateWorkItems() {
            AssertInvariant();
            ImageOrigin focus = focusedImage;
            var desiredCache = new List<CacheEntry>();

            // UNDONE:
            // create a deep copy of unpredictedRequests so desiredCache doesn't contain
            // any items that are entry.state != CacheEntryState.Pending, confusing
            // the eventual merge
            var unpredictedCopy = unpredictedRequests;//.Select(ce => (CacheEntry) ce.Clone());
            //var unpredictedCopy = unpredictedRequests.Select(ce => (CacheEntry) ce.Clone());
            desiredCache.AddRange(unpredictedCopy);

            // in priority order
            if (this.PrefetchPolicy == PrefetchPolicy.Slideshow) {
                int focusIndex = ImageOrigin.GetIndex(imageOrigins, focus);
                // Full-screen image being displayed
                if (focus != null) {
                    desiredCache.Add(new CacheEntry(new LoadRequest(focus, clientWidth, clientHeight, ScalingBehavior.Full)));
                }

                // prefetch of next full-screen images
                for (int i = 1; i <= Lookahead; i++) {
                    desiredCache.Add(new CacheEntry(new LoadRequest(ImageOrigin.NextImage(imageOrigins, focusIndex, +i), clientWidth, clientHeight, ScalingBehavior.Full)));
                }

                // previous full-screen images
                for (int i = 1; i <= Lookbehind; i++) {
                    desiredCache.Add(new CacheEntry(new LoadRequest(ImageOrigin.NextImage(imageOrigins, focusIndex, -i), clientWidth, clientHeight, ScalingBehavior.Full)));
                }
            } else if (this.PrefetchPolicy == PrefetchPolicy.PhotoGrid) {
                desiredCache.AddRange(CreateCacheForPhotoGridCachePolicy());
            } else if (this.PrefetchPolicy == PrefetchPolicy.PageDesigner) {
                BookModel book = RootControl.Instance.book;
                PhotoPageModel page = book.SelectedPage;

                if (page != null) {
                    desiredCache.AddRange(CreateCacheForBookPage(page, clientWidth, clientHeight, ScalingBehavior.Small));

                    // next + prev page
                    int pageNum = book.Pages.IndexOf(page);
                    if (pageNum < book.Pages.Count - 1)
                        desiredCache.AddRange(CreateCacheForBookPage(book.Pages[pageNum + 1], clientWidth, clientHeight, ScalingBehavior.Small));
                    if (pageNum > 0)
                        desiredCache.AddRange(CreateCacheForBookPage(book.Pages[pageNum - 1], clientWidth, clientHeight, ScalingBehavior.Small));
                }

                foreach (var p in book.Pages) {
                    desiredCache.AddRange(CreateCacheForBookPage(p, 125, 125, ScalingBehavior.Small));
                }

                desiredCache.AddRange(CreateCacheForPhotoGridCachePolicy());
            }
            else {
                Debug.Fail("What other kind of loader mode is there?");
            }

            desiredCache = desiredCache.Where(c => c.request.origin != null).Distinct().ToList(); // in case the imageOrigins collection is small and lookahead wraps around and catches lookbehind

            var newCache = new List<CacheEntry>();
            // Update any existing entries that correspond to the things we want.
            // If the new thing isn't in the existing cache, add it.
            // If it is in the existing cache, cancel any associated work items and requeue them to reflect our new priorities.
            foreach (var entry in desiredCache) {
                CacheEntry existing = cache.Find((x) => x.Equals(entry) && x.state != CacheEntryState.Aborted);
                CacheEntry newEntry = null;
                if (existing == null)
                {
                    // image wasn't previously requested
                    newEntry = entry;
                    QueueWorkItem(entry);
                } else if (CacheEntryState.Pending == CompareExchange(target: ref existing.state,
                   newValue: CacheEntryState.Aborted, expectedOldValue: CacheEntryState.Pending))
                {
                    // Abort the existing entry & create a new one so we can use updated priorities.
                    newEntry = entry;
                    newEntry.CompletedCallbacks = existing.CompletedCallbacks;
                    existing.workitem.Cancel();
                    QueueWorkItem(entry);

                    if (existing != null)
                        Debug.Assert(existing.CompletedCallbacks.Count == newEntry.CompletedCallbacks.Count);
                } else if (existing.state == CacheEntryState.Aborted)
                {
                    Debug.Fail("Why is this still in the cache?");
                    // TODO: de-dupe the entry in case the same image is requested twice
                }
                else // done, InProgress
                {
                    newEntry = existing;
                }
            
                if (existing != null)
                    Debug.Assert(existing.CompletedCallbacks.Count == newEntry.CompletedCallbacks.Count);

                // UNDONE: can be more clever about lower resolution requests when you already have a higher resolution
                Assert(newEntry != null);
                newCache.Add(newEntry);
            }

            // Now cancel any remaining work items (ie, everything we didn't touch above)
            foreach (var entry in this.cache) {
                CacheEntry desired = desiredCache.Find((x) => x.Equals(entry));
                if (desired == null) {
                    entry.workitem.Cancel();
                }
            }

            this.cache = newCache;
            cacheLookup = cache.ToLookup((x) => x.request.origin);
            AssertInvariant();
        }

        // Calculate all the CacheEntry required for the photogrid.
        private IEnumerable<CacheEntry> CreateCacheForPhotoGridCachePolicy() {
            var cache = new List<CacheEntry>();
            int firstIndex = ImageOrigin.GetIndex(imageOrigins, FirstThumbnail);

            // thumbnails currently displayed + one more page
            for (int i = 0; i <= ThumbnailsPerPage * 2; i++) {
                cache.Add(new CacheEntry(new LoadRequest(ImageOrigin.NextImage(imageOrigins, firstIndex, +i), 
                    125, 125, ScalingBehavior.Thumbnail)));
            }

            // thumbnails for previous page
            for (int i = 0; i <= ThumbnailsPerPage; i++) {
                cache.Add(new CacheEntry(new LoadRequest(ImageOrigin.NextImage(imageOrigins, firstIndex, -i), 
                    125, 125, ScalingBehavior.Thumbnail)));
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
            return cache;
        }

        // Creates cache entries for the given book page.
        // width and height are the physical pixels to decode the resulting images to.
        // TODO: figure out a way to calculate the right number of pixels for each image. 
        // Currently, it's one-size-fits-all -- if there's multiple images on the page,
        // they're all decoded to the same size, which is typically the page size.
        private IEnumerable<CacheEntry> CreateCacheForBookPage(PhotoPageModel page, int width, int height, ScalingBehavior scalingBehavior)
        {
            var res = page.Images.Select(i => new CacheEntry(new LoadRequest(i, width, height, scalingBehavior)));
            return res;
        }

        // Add a work item to the thread pool to decode the image in the request parameter.
        private void QueueWorkItem(CacheEntry entry) {
            Assert(entry.state == CacheEntryState.Pending);
            entry.workitem = smartThreadPool.QueueWorkItem(
                new WorkItemCallback((object ignored) => { DecodeImageOnBackgroundThread(entry); return null; }),
                WorkItemPriority.BelowNormal);
        }

        public void Shutdown()
        {
            // TODO -- support shutting down a ImageLoader.
            // Currently irrelevant because the only time we need to shut it down is right before process end.
        }

        // Load/decode the requested image synchronously, blocking the UI
        // thread and ignoring the cache.
        // This is useful for printing.
        // width and height are the physical pixels to decode the image to.
        public ImageInfo LoadSync(LoadRequest request) {
            ImageInfo info = ImageInfo.Load(request);
            return info;
        }

        // Asynchronously load an image that was not anticipated by the prefetch policy,
        // and invoke the completed callback when done.
        // width and height are the physical pixels to decode the image to.
        // not used at the moment
        public void BeginLoadUnpredicted(LoadRequest request, Action<ImageInfo> onCompleted)
        {
            //Debug.WriteLine("" + width + " " + height);
            BeginLoad(request, onCompleted, true);
        }

        // Asynchronously load an image that was not anticipated by the prefetch policy,
        // and invoke the onCompleted callback when done.
        // width and height are the physical pixels to decode the image to.
        public void BeginLoad(LoadRequest request, Action<ImageInfo> onCompleted, bool unpredicted = false)
        {
            AssertInvariant();
            Debug.Assert(onCompleted != null);
            IEnumerable<CacheEntry> entries = cacheLookup[request.origin].Where((x) =>
                x.request.origin.Equals(request.origin) 
                && (x.request.height >= request.height || x.request.width >= request.width) 
                && x.request.scalingBehavior == request.scalingBehavior
                );

            if (!unpredicted && entries.Count() == 0) {
                // hack: we're here because we gave 'em a thumbnail when they asked for a small
                // retry w/o height/width requirement
                entries = cacheLookup[request.origin].Where((x) =>
                    x.request.origin.Equals(request.origin) 
                    && x.request.scalingBehavior == request.scalingBehavior
                );
                if (entries.Count() == 0)
                    throw new Exception("Request for unexpected image; loader mode must be wrong");
            }

            CacheEntry entry;
            if (entries.Count() > 0) {
                entry = entries.First();
            } else { // create entry
                Debug.Assert(unpredicted);
                entry = new CacheEntry(request);
                unpredictedRequests.Add(entry);
                UpdateWorkItems();
            }

            entry.AssertInvariant();
            if (entry.info != null) {
                // Image already loaded.
                // Invokes the callback asynchronously to avoid changing the event order
                // in the case the item is in the cache
                mainDispatcher.BeginInvoke(
                    new System.Action(() => {
                        // todo: Why fire just this one onCompleted callback rather than all the callbacks associated with the entry?
                        RaiseLoaded(onCompleted, entry);
                    }));
            } else {
                entry.CompletedCallbacks.Add(onCompleted);
                // no race condition, when the threadpool completes, it will call the UI thread to 
                // clean up the Requesters list
            }
            AssertInvariant();
        }

        // Removes the image from the unpredicted requests list, 
        // and calls the onCompleted callback. Runs synchronously.
        private void RaiseLoaded(Action<ImageInfo> onCompleted, CacheEntry entry)
        {
            if (unpredictedRequests.Contains(entry))
                unpredictedRequests.Remove(entry);
            onCompleted(entry.info);
        }

        // Decode the image and call back to the UI thread when done.
        // This function is supposed to be called on the background thread,
        // not the UI thread. It's the only function in this file
        // that's called on the background thread.
        private void DecodeImageOnBackgroundThread(CacheEntry entry /*, Dispatcher uiThreadDispatcher*/) {
            // Abort if state=abort, otherwise set state=InProgress
            if (CacheEntryState.Pending != CompareExchange(ref entry.state,
                newValue: CacheEntryState.InProgress, expectedOldValue: CacheEntryState.Pending))
            {
                Debug.Assert(entry.state == CacheEntryState.Aborted);
                return;
            }

            ImageInfo info = ImageInfo.Load(entry.request);
            //Debug.Assert(info.scaledSource != null);
            
            // send answer back to UI thread
            var callback = new LoadCompletedCallback(this.OnLoadCompleted);
            mainDispatcher.BeginInvoke(callback, DispatcherPriority.Background, entry, info);
        }

        // Interlocked.CompareExchange doesn't work on enums w/o a little finessing.
        // from https://stackoverflow.com/questions/18358518/interlocked-compareexchange-with-enum
        private static unsafe CacheEntryState CompareExchange(ref CacheEntryState target, 
            CacheEntryState newValue, CacheEntryState expectedOldValue)
        {
            return (CacheEntryState)Interlocked.CompareExchange(
                                        ref Unsafe.As<CacheEntryState, int>(ref target),
                                        (int)newValue,
                                        (int)expectedOldValue); 
        }

        // Called on the UI thread when the CacheEntry has finished fetching.
        // This method sends the completed event for CacheEntry.Loaded, but not before
        // getting other dispatcher items a chance to run.
        // 
        // Trying to finesse queue prioritization.
        // If you set the priority high, and there's a whole lot of thumbnails that load fast enough,
        // you get into a situation where it never renders because just as it's finishing up laying out, another thumbnail 
        // comes along and invalidates everything.  On the other hand, if you naively put the priority low, you'll run 
        // layout once for every thumbnail no matter how fast they come in.
        // Here we attempt to batch them up, but once we start a layout we try to always render it.
        private void OnLoadCompleted(CacheEntry entry, ImageInfo info)
        {
            pendingLoadedEvents.Add(new LoadedPartiallyCompleted() { entry = entry, info = info });
            var callback = new Action(this.FirePendingLoadedRequests);
            mainDispatcher.BeginInvoke(callback, DispatcherPriority.Normal);
        }

        // A list of loaded events to fire after giving other dispatcher items a chance to run
        private List<LoadedPartiallyCompleted> pendingLoadedEvents = new List<LoadedPartiallyCompleted>();

        private class LoadedPartiallyCompleted {
            public CacheEntry entry;
            public ImageInfo info;
        }

        private delegate void LoadCompletedCallback(CacheEntry entry, ImageInfo info);

        private void FirePendingLoadedRequests()
        {
            foreach (var partial in pendingLoadedEvents) {
                CacheEntry entry = partial.entry;
                ImageInfo info = partial.info;
                AssertInvariant();
                entry.info = info;
                entry.state = CacheEntryState.Done;
                foreach (var completedCallback in entry.CompletedCallbacks) {
                    RaiseLoaded(completedCallback, entry);
                }
                entry.CompletedCallbacks.Clear();
                entry.AssertInvariant();
                AssertInvariant();
            }
            pendingLoadedEvents.Clear();
        }
    }
}