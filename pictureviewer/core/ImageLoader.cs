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

namespace Pictureviewer.Core
{
    class LoadedEventArgs : EventArgs
    {
        public LoadedEventArgs(ImageInfo info, object requester)
        {
            this.ImageInfo = info;
            this.Requester = requester;
        }

        public ImageInfo ImageInfo;
        public object Requester;
    }

    delegate void LoadedEventHandler(object sender, LoadedEventArgs args);

    internal enum LoaderMode {
        Slideshow, 
        PhotoGrid, 
        PageDesigner
    }

    // this class handles caching as well as offloading work to background threads.
    internal class ImageLoader
    {
        private static void Assert(bool condition) {
            Assert(condition, "");
        }
        private static void Assert(bool condition, string message) {
            if (!condition) {
                Debug.Fail(message);
            }
        }

        private enum CacheEntryState {
            Pending, InProgress, Done
        }

        private class CacheEntry : ICloneable {
            public CacheEntry(ImageOrigin origin, int width, int height, ImageResolution resolution) {
                this.origin = origin;
                this.height = height;
                this.width = width;
                this.resolution = resolution;
            }

            public readonly int width; // in pixels
            public readonly int height;
            public readonly ImageResolution resolution;
            public readonly ImageOrigin origin;

            public ImageInfo info;

            // Parties we need to notify when the image is ready
            public List<Action<ImageInfo>> CompletedCallbacks = new List<Action<ImageInfo>>();
            public IWorkItemResult workitem;
            public CacheEntryState state = CacheEntryState.Pending;

            public void AssertInvariant() {
                Assert(origin != null);
                if (CompletedCallbacks.Count > 0) {
                    Assert(info == null);
                    // better not have people waiting if you already know the answer
                }
                if (info != null) {
                    Assert(state == CacheEntryState.Done);
                    // there is a brief timing window where workitem.IsCompleted but the info & State properties haven't been updated yet
                }
                if (state == CacheEntryState.Done)
                    Assert (info != null);
            }

            public override bool Equals(object obj) {
                if (obj is CacheEntry) {
                    var entry = obj as CacheEntry;
                    return this.origin == entry.origin && this.width == entry.width && this.height == entry.height && this.resolution == entry.resolution;
                } else
                    return false;
            }
            public override int GetHashCode() {
                return this.origin.SourcePath.GetHashCode(); // UNDONE: isn't the greatest hash function in the world but it's correct...
            }

            public override string ToString() {
                return origin.DisplayName + " " + state + " " + width+"x"+height+" " + resolution;
            }

            public object Clone() {
                CacheEntry entry = (CacheEntry)this.MemberwiseClone();
                entry.CompletedCallbacks = new List<Action<ImageInfo>>();
                entry.workitem = null;
                entry.state = CacheEntryState.Pending;
                return entry;
            }
        }

        private LoaderMode mode = LoaderMode.Slideshow;

        public LoaderMode Mode
        {
            get { return mode; }
            set { mode = value; UpdateWorkItems(); }
        }

        public readonly int Lookahead = 3;
        public readonly int Lookbehind = 2;
        public int ThumbnailsPerPage = 0; // approximate -- doesn't need to be 100% accurate
        public ImageOrigin FirstThumbnail = null;
        private ImageOrigin[] imageOrigins = new ImageOrigin[0];
        private Dispatcher mainDispatcher;
        private ImageOrigin focusedImage = null;
        private List<CacheEntry> cache = new List<CacheEntry>();
        // possibly a premature optimization for form startup time
        private ILookup<ImageOrigin, CacheEntry> cacheLookup;
        private List<CacheEntry> unpredictedRequests = new List<CacheEntry>(); // Requests that weren't anticipated
        private SmartThreadPool smartThreadPool = new SmartThreadPool();
        private int clientHeight;
        private int clientWidth;

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

        public ImageLoader() {
            this.smartThreadPool.MaxThreads = Environment.ProcessorCount;
            this.mainDispatcher = Dispatcher.CurrentDispatcher;
            AssertInvariant();
        }

        public void SetTargetSize(int width, int height) {
            this.clientWidth = width;
            this.clientHeight = height;
            UpdateWorkItems();
        }

        public void SetImageOrigins(ImageOrigin[] imageOrigins, ImageOrigin focusedImage) {
            Assert(focusedImage == null || imageOrigins.Contains(focusedImage));
            if (focusedImage == null && imageOrigins.Length > 0)
                focusedImage = imageOrigins[0];

            this.focusedImage = focusedImage;
            this.imageOrigins = imageOrigins;
            UpdateWorkItems();
        }

        public void SetFocus(ImageOrigin focusedImage) {
            Assert(focusedImage == null || imageOrigins.Contains(focusedImage));
            if (focusedImage == null && imageOrigins.Length > 0)
                focusedImage = imageOrigins[0];

            this.focusedImage = focusedImage;
            UpdateWorkItems();
        }

        private IEnumerable<CacheEntry> CachesForPage(PhotoPageModel page, int width, int height, ImageResolution resolution) {
            var res = page.Images.Select(i => new CacheEntry(i, width, height, resolution));
            return res;
        }


        // HACK: shouldn't be public
        public void UpdateWorkItems() {
            AssertInvariant();
            ImageOrigin focus = focusedImage;
            var desiredCache = new List<CacheEntry>();

            // UNDONE:
            // create deep copy of unpredictedRequests so desiredCache doesn't contain
            // any items that are entry.state != CacheEntryState.Pending, confusing
            // the eventual merge
            var unpredictedCopy = unpredictedRequests;//.Select(ce => (CacheEntry) ce.Clone());
            //var unpredictedCopy = unpredictedRequests.Select(ce => (CacheEntry) ce.Clone());
            desiredCache.AddRange(unpredictedCopy);

            // in priority order
            if (this.Mode == LoaderMode.Slideshow) {
                int focusIndex = ImageOrigin.GetIndex(imageOrigins, focus);
                // Full-screen image being displayed
                if (focus != null) {
                    desiredCache.Add(new CacheEntry(focus, clientWidth, clientHeight, ImageResolution.Full));
                }

                // prefetch of next full-screen images
                for (int i = 1; i <= Lookahead; i++) {
                    desiredCache.Add(new CacheEntry(ImageOrigin.NextImage(imageOrigins, focusIndex, +i), clientWidth, clientHeight, ImageResolution.Full));
                }

                // previous full-screen images
                for (int i = 1; i <= Lookahead; i++) {
                    desiredCache.Add(new CacheEntry(ImageOrigin.NextImage(imageOrigins, focusIndex, -i), clientWidth, clientHeight, ImageResolution.Full));
                }
            } else if (this.Mode == LoaderMode.PhotoGrid) {
                PhotoGridCachePolicy(desiredCache);
            } else if (this.Mode == LoaderMode.PageDesigner) {
                BookModel book = RootControl.Instance.book;
                PhotoPageModel page = book.SelectedPage;
                
                if (page != null) {
                    desiredCache.AddRange(CachesForPage(page, clientWidth, clientHeight, ImageResolution.Small));

                    // next + prev page
                    int pageNum = book.Pages.IndexOf(page);
                    if (pageNum < book.Pages.Count - 1)
                        desiredCache.AddRange(CachesForPage(book.Pages[pageNum + 1], clientWidth, clientHeight, ImageResolution.Small));
                    if (pageNum > 0)
                        desiredCache.AddRange(CachesForPage(book.Pages[pageNum - 1], clientWidth, clientHeight, ImageResolution.Small));
                }

                foreach (var p in book.Pages) {
                    desiredCache.AddRange(CachesForPage(p, 125, 125, ImageResolution.Small));
                }

                PhotoGridCachePolicy(desiredCache);
            } else {
                Debug.Fail("What other kind of loader mode is there?");
            }

            desiredCache = desiredCache.Where(c => c.origin != null).Distinct().ToList(); // in case the imageOrigins collection is small and lookahead wraps around and catches lookbehind

            var newCache = new List<CacheEntry>();
            // Update any existing entries that correspond to the things we want.
            // If the new thing isn't in the existing cache, add it.
            // If it is in the existing cache, cancel any associated work items and requeue them to reflect our new priorities
            foreach (var entry in desiredCache) {
                CacheEntry existing = cache.Find((x) => x.Equals(entry));
                CacheEntry newEntry = null;
                if (existing == null) {
                    newEntry = entry;
                    QueueWorkItem(entry);
                } else if (existing.state == CacheEntryState.Done || existing.state == CacheEntryState.InProgress) {
                    newEntry = existing;
                } else {

                    // Delete the existing work item & create a new one so we can use updated priorities
                    Assert(existing.state == CacheEntryState.Pending);
                    newEntry = entry;
                    newEntry.CompletedCallbacks = existing.CompletedCallbacks;
                    // BUG: race condition: the entry starts running now. Thread pool would finish the work item like it should, however
                    // we'll end up with multiple cache entries and will load the image twice
                    existing.workitem.Cancel();
                    QueueWorkItem(entry);

                    if (existing != null)
                        Debug.Assert(existing.CompletedCallbacks.Count == newEntry.CompletedCallbacks.Count);
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
            cacheLookup = cache.ToLookup((x) => x.origin);
            AssertInvariant();
        }

        private void PhotoGridCachePolicy(List<CacheEntry> desiredCache) {
            int firstIndex = ImageOrigin.GetIndex(imageOrigins, FirstThumbnail);

            // thumbnails currently displayed + one more page
            for (int i = 0; i <= ThumbnailsPerPage * 2; i++) {
                desiredCache.Add(new CacheEntry(ImageOrigin.NextImage(imageOrigins, firstIndex, +i), 
                    125, 125, ImageResolution.Thumbnail));
            }

            // thumbnails for previous page
            for (int i = 0; i <= ThumbnailsPerPage; i++) {
                desiredCache.Add(new CacheEntry(ImageOrigin.NextImage(imageOrigins, firstIndex, -i), 
                    125, 125, ImageResolution.Thumbnail));
            }

            // larger images for grid currently displayed
            //for (int i = 0; i <= ThumbnailCount; i++) {
            //    //desiredCache.Add(new CacheEntry(imageOrigins[SlideShow.NextIndex(imageOrigins, origin.Index, +i)], 125, 125, ImageResolution.Small));//new Size(clientWidth, clientHeight)));
            //}

            // Full-screen image being displayed -- Really we just want to not evict whatever's there in the cache,
            // ideally we wouldn't actively load it
            //if (focus != null)
            //{
            //    desiredCache.Add(new CacheEntry(focus, clientWidth, clientHeight, ImageResolution.Full));
            //}
        }

        private void QueueWorkItem(CacheEntry entry) {
            // bug: uncomment Assert when race condition fixed
            Assert(entry.state == CacheEntryState.Pending);
            entry.workitem = smartThreadPool.QueueWorkItem(
                new WorkItemCallback((object ignored) => { BackgroundBeginLoad(entry); return null; }),
                WorkItemPriority.BelowNormal);
        }

        public void ClearCache() {
            // UNDONE
        }

        public void Shutdown() {
            // UNDONE
        }

        public ImageInfo LoadSync(ImageOrigin origin, int width, int height, ImageResolution resolution) {
            // ignores cache
            ImageInfo info = ImageInfo.Load(origin, width, height, resolution);
            return info;
        }

        // not used at the moment
        public void BeginLoadUnpredicted(ImageOrigin origin, int width, int height, ImageResolution resolution, Action<ImageInfo> completed)
        {
            //Debug.WriteLine("" + width + " " + height);
            BeginLoadInternal(origin, width, height, resolution, completed, true);
        }

        public void BeginLoad(ImageOrigin origin, int width, int height, ImageResolution resolution, Action<ImageInfo> completed)
        {
            BeginLoadInternal(origin, width, height, resolution, completed, false);
        }

        private void BeginLoadInternal(ImageOrigin origin, int width, int height, ImageResolution resolution, Action<ImageInfo> completed, bool unpredicted)
        {
            AssertInvariant();
            Debug.Assert(completed != null);
            IEnumerable<CacheEntry> entries = cacheLookup[origin].Where((x) =>
                x.origin.Equals(origin) && (x.height >= height || x.width >= width) && x.resolution == resolution
                );

            if (!unpredicted && entries.Count() == 0) {
                // hack: we're here because we gave 'em a thumbnail when they asked for a small
                // retry w/o height/width requirement
                entries = cacheLookup[origin].Where((x) =>
                    x.origin.Equals(origin) && x.resolution == resolution
                );
                if (entries.Count() == 0)
                    throw new Exception("Request for unexpected image; loader mode must be wrong");
            }

            CacheEntry entry;
            if (entries.Count() > 0) {
                entry = entries.First();
            } else { // create entry
                Debug.Assert(unpredicted);
                entry = new CacheEntry(origin, width, height, resolution);
                unpredictedRequests.Add(entry);
                UpdateWorkItems();
            }

            entry.AssertInvariant();
            if (entry.info != null) {
                // Invokes the callback asynchronously to avoid changing the event order
                // in the case the item is in the cache
                mainDispatcher.BeginInvoke(
                    new System.Action(() => {
                        RaiseLoaded(completed, entry);
                    }));
            } else {
                entry.CompletedCallbacks.Add(completed);
                // no race condition, when the threadpool completes, it will call the UI thread to 
                // clean up the Requesters list
            }
            AssertInvariant();
        }

        private void RaiseLoaded(Action<ImageInfo> completed, CacheEntry entry)
        {
            if (unpredictedRequests.Contains(entry))
                unpredictedRequests.Remove(entry);
            completed(entry.info);
        }

        public bool IsTriageMode; // whether to update selection based on file existence

        // for debugging only
        //public event LoadedEventHandler PreloadComplete;

        // Because new requests can supersede old ones,
        // we don't process the request right away, rather we
        // delay processing until the message queue has been idle for a few 
        // moments (meaning new requests have stopped coming in)
        private void BackgroundBeginLoad(CacheEntry entry) {
            entry.state = CacheEntryState.InProgress;
            ImageInfo info = ImageInfo.Load(entry.origin,
                entry.width,
                entry.height,
                entry.resolution);
            //Debug.Assert(info.scaledSource != null);
            
            // send answer back to UI thread
            var callback = new LoadCompletedCallback(this.LoadCompletedPart1);
            mainDispatcher.BeginInvoke(callback, DispatcherPriority.Background, entry, info);
        }

        // Trying to finesse queue prioritization.
        // If you set the priority high, and there's a whole lot of thumbnails that load fast enough,
        // you get into a situation where it never renders because just as it's finishing up laying out, another thumbnail 
        // comes along and invalidates everything.  On the other hand, if you na�vely put the priority low, you'll run 
        // layout once for every thumbnail no matter how fast they come in.
        // here we attempt to batch them up, but once we start a layout we try to always render it.
        private List<LoadedPartiallyCompleted> pendingLoadedEvents = new List<LoadedPartiallyCompleted>();

        private class LoadedPartiallyCompleted {
            public CacheEntry entry;
            public ImageInfo info;
        }

        private delegate void LoadCompletedCallback(CacheEntry entry, ImageInfo info);

        private void LoadCompletedPart1(CacheEntry entry, ImageInfo info) {
            pendingLoadedEvents.Add(new LoadedPartiallyCompleted() { entry = entry, info = info });
            var callback = new Action(this.LoadCompletedPart2);
            mainDispatcher.BeginInvoke(callback, DispatcherPriority.Normal);
        }

        private void LoadCompletedPart2()
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