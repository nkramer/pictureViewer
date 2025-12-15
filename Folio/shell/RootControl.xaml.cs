#nullable disable
using Folio.Book;
using Folio.Core;
using Folio.Importer;
using Folio.Slides;
using Folio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Path = System.IO.Path;

namespace Folio.Shell; 
// Represents a full-screen UI. Basically a navigation construct.
public interface IScreen {
    void Activate(ImageOrigin focus); // focus is usually null 
    void Deactivate();
}

// The root of all UI except for the window itself. Contains IScreens.
// And also provides global commands, and holds onto the core data about the image catalog.
public partial class RootControl : UserControl, INotifyPropertyChanged {
    public static string picDir = @"C:\Users\nickk\OneDrive\photo collections\Pictures";
    public static string picDrive = @"c:\";
    public static string dbDir = @"C:\Users\nickk\source\psedbtool";
    public static string dbDirCopy = @"C:\Users\nickk\source\pictureDatabase";
    private static string[] rootDirs = new String[] { picDir, @"C:\old-hdd-3tb\Pictures", @"C:\old-hdd-3tb\All Pictures", @"E:\pictures\Random Pictures", @"C:\old-hdd-3tb\Good Pictures" };

    // Import directories
    public static string ImportDestinationRoot = @"C:\Users\nickk\OneDrive\photo collections\Pictures";
    public static string SdCardRoot = @"F:\DCIM";
    public static string DownloadsRoot = @"C:\Users\nickk\Downloads";

    private bool startInDesignbookMode = false;
    //private bool startInDesignbookMode = true;

    public static RootControl Instance;

    // All top-level tags
    public ObservableCollection<PhotoTag> Tags;

    // Currently applied filters
    // todo: change to ReadOnlyObservableCollection & make readonly
    public ObservableCollection<PhotoTag> AllOfTags = new ObservableCollection<PhotoTag>();
    public ObservableCollection<PhotoTag> AnyOfTags = new ObservableCollection<PhotoTag>();
    public ObservableCollection<PhotoTag> ExcludeTags = new ObservableCollection<PhotoTag>();

    private List<PhotoTag> tagUndoStack = new List<PhotoTag>();

    // The selected photo set is the intersection of displayset & selected.
    // The tags are the ones shared by all selected photos (ie, intersection)
    public ObservableCollection<PhotoTag> SelectedPhotoTags = new ObservableCollection<PhotoTag>();

    // All photos that pass the applied filters. May not actually be visible on the screen.
    private ImageOrigin[] displaySet = new ImageOrigin[0];

    // All known photos
    private ImageOrigin[] completeSet = new ImageOrigin[0];

    private ImageOrigin focusedImage;

    internal CommandHelper commands;
    internal FileListSource fileListSource;
    internal ImageLoader loader = new ImageLoader();

    // HACK: seems easier to implement INotifyPropertyChanged than make everything a dependency property
    public event PropertyChangedEventHandler PropertyChanged;

#if WPF
    private Window window;
    private ContextMenu contextmenu = new ContextMenu();
#endif

    private Folio.Library.PhotoGrid photoGrid;

    public bool changesToSave = false;

    public BookModel book = null;
    public string currentBookPath = null; // Track the currently loaded book path (session only)

    private string GetMostRecentDatabase(out string tagFile) {
        List<string> files = Directory.GetFiles(dbDir, "*.csv").ToList();
        files.Sort();
        string mainFile = files[files.Count - 1];  // .Net 4.8 and .Net 8 sort these in different order
        tagFile = files[files.Count - 2];
        Debug.Assert(tagFile.ToLower().EndsWith("_tag_defs.csv"));
        return mainFile;
    }

    public RootControl() {
        //new SuperGrid();

        Debug.Assert(Instance == null);
        Instance = this;

        InitializeComponent();

        this.GotFocus += new RoutedEventHandler(RootControl_GotFocus);
        this.LostFocus += new RoutedEventHandler(RootControl_LostFocus);

        string tagFile;
        string mainFile = GetMostRecentDatabase(out tagFile);
        Dictionary<string, PhotoTag> tagLookup;
        this.Tags = PhotoTag.Parse(File.ReadAllLines(tagFile), out tagLookup);
        var origins = ImageOrigin.Parse(File.ReadAllLines(mainFile), tagLookup);
        //origins = ImageOrigin.Parse(File.ReadAllLines(mainFile), tagLookup);

#if DEBUG
        // roundtrip test -- slow
        //ImageOrigin.Parse(ImageOrigin.Persist(origins), tagLookup);
#endif

        this.fileListSource = new DesktopFileListSource();
        fileListSource.rootWindow = this.window;
        this.commands = new CommandHelper(this);

#if WPF
        this.ContextMenu = contextmenu;
        commands.contextmenu = contextmenu;

        if (Application.Current.Windows.Count > 0) {
            // Tests don't go down this path
            this.window = Application.Current.Windows[0];
            window.WindowStyle = WindowStyle.None;
            window.WindowState = WindowState.Maximized;
            window.ResizeMode = ResizeMode.NoResize;
            //window.Closing += new System.ComponentModel.CancelEventHandler(SlideShow_Closing);
            //window.Deactivated += new EventHandler(SlideShow_Deactivated);
        }

        this.Focusable = true;
        this.FocusVisualStyle = null;
#else
        //this.fileListSource = new WebServiceFileListSource();
        this.fileListSource = new HardCodedFileListSource();
        // When you switch between these two file sources, keep in mind that 
        // you also need to update ImageInfoSilverlight.cs and that with the 
        // hardcoded one, you don't need a Web _service_ but you do need a 
        // web _server_ because Silverlight doesn't allow networking from apps
        // initiated from file:// url's.
#endif
        CreateCommands();
        this.PropertyChanged += new PropertyChangedEventHandler(RootControl_PropertyChanged);

        AllOfTags.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Tags_CollectionChanged);
        AnyOfTags.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Tags_CollectionChanged);
        ExcludeTags.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Tags_CollectionChanged);

        // import new good/better/best
        //IEnumerable<ImageOrigin> addedOrigins = ImportGoodBetterBest(tagLookup, origins);
        //AutoTagDatesAndPlaces(addedOrigins);
        //List<ImageOrigin> newSet = origins.Concat(addedOrigins).ToList();
        List<ImageOrigin> newSet = origins.ToList();

        newSet.Sort(new ImageOrigin.OriginComparer());
        this.SetCompleteSet(newSet.ToArray(), newSet.First());

        // let PhotoGrid init the loader
        // loader.Mode = LoaderMode.Thumbnail;
        this.photoGrid = new Folio.Library.PhotoGrid(this);
        PushScreen(photoGrid);

        // unnecessary?
        //photoGrid.Loaded += (sender, args) => {
        //if (!startInDesignbookMode)
        //    photoGrid.Focus();
        //};

        photoGrid.Exited += (sender, args) => {
            PushScreen(new SlideShow(this));
        };

        this.Loaded += new RoutedEventHandler(OnLoaded);

        if (startInDesignbookMode) {
            PushScreen(new PageDesigner());
        }

        //this.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.ContextIdle);
        //this.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        //DebugOpenPageDesigner();
        //var pd = (TopScreen as PageDesigner);
        //pd.ShowTemplateChooser();

        //DebugOpenTemplateChooser();

        //var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        //dispatcherTimer.Tick += (o,e) => { DebugOpenTemplateChooser(); };
        //dispatcherTimer.Interval = new TimeSpan(0,0,0,0, 10000);
        //dispatcherTimer.Start();
    }

    //private void DebugOpenPageDesigner() {
    //    this.Dispatcher.BeginInvoke(new ThreadStart(() => {
    //        PopScreen();
    //        PushScreen(new PageDesigner());
    //        debugRepeatcount++;
    //        if (debugRepeatcount < 5) {
    //            DebugOpenPageDesigner();
    //        } else {
    //            this.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    //        }
    //    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    //}

    //private DateTime debugStartTime;

    //private void DebugOpenTemplateChooser() {
    //    this.Dispatcher.BeginInvoke(new ThreadStart(() => {
    //        if (debugRepeatcount == 0)
    //            debugStartTime = DateTime.Now;

    //        var pd = (TopScreen as PageDesigner);
    //        pd.ShowTemplateChooser();
    //        debugRepeatcount++;
    //        if (debugRepeatcount < 60) {
    //            DebugOpenTemplateChooser();
    //        } else {
    //            TimeSpan ellapsedTime = debugStartTime - DateTime.Now;
    //            Debug.WriteLine("ellapsedTime = " + ellapsedTime);
    //            File.WriteAllText(@"C:\html\ellapsedTime.txt", "ellapsedTime = " + ellapsedTime);
    //            this.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    //        }
    //    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    //}

    //private int debugRepeatcount = 0;

    private static void AutoTagDatesAndPlaces(IEnumerable<ImageOrigin> origins, ObservableCollection<PhotoTag> allTags) {
        foreach (ImageOrigin i in origins) {
            String filename = Path.GetFileName(i.SourcePath);
            if (filename.StartsWith("20")) { // eg 2011-03-09
                int space = filename.IndexOf(' ');
                string date = filename.Substring(0, space);
                int lastSpace = filename.LastIndexOf(' ');
                string description = (space == lastSpace) ? "" : filename.Substring(space + 1, lastSpace - space - 1);
                string year = date.Split('-')[0];
                string month = date.Split('-')[1];
                if (month.Length < 2) month = "0" + month;

                i.AddTag(PhotoTag.FindOrMake("Dates|Year|" + year, allTags));
                i.AddTag(PhotoTag.FindOrMake("Dates|Month|" + month, allTags));
                i.AddTag(PhotoTag.FindOrMake("Places|WA|" + description, allTags));
            }
        }
        // debug helper:
        // var malformed = CompleteSet.Where(o => !Path.GetFileName(o.SourcePath).StartsWith("20")).ToList();
    }

    private static void CopyMatchingRawFilesIfAvailable(IEnumerable<ImageOrigin> origins) {
        var fileList1 = origins.Select(o => Path.GetFileNameWithoutExtension(o.DisplayName.ToLower()) + ".cr2").ToArray();
        var fileList2 = origins.Select(o => Path.GetFileNameWithoutExtension(o.DisplayName.ToLower()) + ".raf").ToArray();
        var fileList3 = origins.Select(o => Path.GetFileNameWithoutExtension(o.DisplayName.ToLower()) + ".arw").ToArray();
        var filesToLookFor = fileList1.Concat(fileList2).Concat(fileList3).ToLookup(str => str);

        var leafDirs = rootDirs.SelectMany(root => Directory.GetDirectories(root)).ToArray();
        var allFiles = leafDirs.SelectMany(dir => Directory.GetFiles(dir)).ToArray();
        var toCopy = allFiles.Where(file => filesToLookFor.Contains(Path.GetFileName(file).ToLower()))
            .ToArray();

        var nameToOrigin = origins.ToLookup(o => Path.GetFileNameWithoutExtension(o.DisplayName).ToLower());
        foreach (string raw in toCopy) {
            var matchingOrigins = nameToOrigin[Path.GetFileNameWithoutExtension(raw).ToLower()];
            Debug.Assert(matchingOrigins.Count() == 1);
            ImageOrigin o = matchingOrigins.First();
            string targetDir = o.SourceDirectory;
            string targetShortName = Path.GetFileName(raw);
            string targetName = Path.Combine(targetDir, targetShortName);
            if (!File.Exists(targetName)) {
                Debug.WriteLine("{0} -> {1}", raw, targetName);
                File.Copy(raw, targetName);
            }
        }
    }


    private static IEnumerable<ImageOrigin> ImportGoodBetterBest(IEnumerable<ImageOrigin> origins,
        ObservableCollection<PhotoTag> allTags) {
        var existingFilenames = origins.ToLookup(o => o.DisplayName);
        var dirs = Directory.GetDirectories(picDir)
            .Where(s => Path.GetFileName(s).StartsWith("good"));
        var allGood = dirs.SelectMany(d => Directory.GetFiles(d));
        var files = allGood
            .Where(p => !existingFilenames.Contains(Path.GetFileName(p)) && (Path.GetExtension(p).ToLower() == ".jpg" || Path.GetExtension(p).ToLower() == ".heic"));

        ImageOrigin[] addedOrigins = files.Select(p => new ImageOrigin(p, null)).ToArray();
        // ToArray is needed to make sure there's exactly one copy

        TagRatedPhotos(addedOrigins, "better", "Rated|**", allTags);
        TagRatedPhotos(addedOrigins, "best", "Rated|***", allTags);
        return addedOrigins;
    }

    private static void TagRatedPhotos(IEnumerable<ImageOrigin> origins, string dirKind, string tag,
        ObservableCollection<PhotoTag> allTags) {
        var dirs = Directory.GetDirectories(picDir).Where(s => Path.GetFileName(s).StartsWith(dirKind));
        var files = dirs.SelectMany(d => Directory.GetFiles(d)).Where(p => (Path.GetExtension(p).ToLower() == ".jpg" || Path.GetExtension(p).ToLower() == ".heic"));
        var l = origins.ToLookup(i => Path.GetFileName(i.SourcePath));
        foreach (var f in files) {
            if (l.Contains(Path.GetFileName(f))) {
                var im = l[Path.GetFileName(f)];
                foreach (var i in im) {
                    i.AddTag(PhotoTag.FindOrMake(tag, allTags));
                }
            } else {
                Debug.WriteLine("file not found: " + f);
            }
        }
    }

    private void RootControl_LostFocus(object sender, RoutedEventArgs e) {
        //FrameworkElement elt = (FrameworkElement)e.OriginalSource;
        //Debug.WriteLine("Lost focus: " + elt.ToString() + " (" + elt.Name + ")");
    }

    private void RootControl_GotFocus(object sender, RoutedEventArgs e) {
        //FrameworkElement elt = (FrameworkElement)e.OriginalSource;
        //Debug.WriteLine("Got focus: " + elt.ToString() + " (" + elt.Name + ")");
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        // We don't hook up this event handler in the constructor because then we would 
        // end up calling ResetLoader twice -- once during normal construction, 
        // and the second time for the size changing.
        clientarea.SizeChanged += new SizeChangedEventHandler(clientarea_SizeChanged);
        InitializeLoader();
        //SelectDirectories(true /* first time*/);
    }

    private void clientarea_SizeChanged(object sender, SizeChangedEventArgs e) {
        SetLoaderTargetSize();
    }

    private void SetLoaderTargetSize() {
        double clientwidth;
        double clientheight;
        ImageDisplay.GetSizeInPhysicalPixels(clientarea, out clientwidth, out clientheight);
        loader.SetTargetSize((int)clientwidth, (int)clientheight);
    }

    private void InitializeLoader() {
        // Delay until here so we can have accurate layout sizes
        SetLoaderTargetSize();
        double clientwidth;
        double clientheight;
        ImageDisplay.GetSizeInPhysicalPixels(clientarea, out clientwidth, out clientheight);
    }

    private void Tags_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
        UpdateFilters();
    }

    public void UpdateFilters() {
        var set1 = completeSet.Where(i => AllOfTags.Count == 0 || AllOfTags.All(t => i.HasTag(t)));
        var set2 = set1.Where(i => AnyOfTags.Count == 0 || AnyOfTags.Any(t => i.HasTag(t)));
        var set3 = set2.Where(i => ExcludeTags.Count == 0 || ExcludeTags.All(t => !i.HasTag(t)));
        var res = set3.ToArray();
        this.DisplaySet = res;

        this.SelectedPhotoTags.Clear();
        // todo: The selected tag display doesn't listen to change notifications (or at least, not nearly enough of them)
        var sel = this.DisplaySet.Where(i => i.IsSelected);
        if (sel.Count() > 0) {
            var tags = sel.Select(i => i.Tags).Aggregate((tags1, tags2)
                => new ObservableCollection<PhotoTag>(tags1.Intersect(tags2)));
            // bug: uses == instead of Matches, and adjust the uniqueness displayed below accordingly

            //.Distinct();
            //var tags = sel.SelectMany(i => i.Tags).Distinct();
            foreach (var t in tags) {
                this.SelectedPhotoTags.Add(t);
            }
        }
    }

    private void RootControl_PropertyChanged(object sender, PropertyChangedEventArgs e) {
        loader.SetImageOrigins(this.DisplaySet, focusedImage);
    }

    public void AddFilter(ObservableCollection<PhotoTag> tags, PhotoTag tag) {
        if (!tags.Contains(tag)) {
            tags.Add(tag);
            tagUndoStack.Add(tag);
        }
    }

    public void RemoveFilter(ObservableCollection<PhotoTag> tags, PhotoTag tag) {
        tags.Remove(tag);
        tagUndoStack.Remove(tag);
    }

    // returns whether there was something to undo
    public bool UndoFilter() {
        if (tagUndoStack.Count > 0) {
            PhotoTag tag = tagUndoStack.Last();
            tagUndoStack.Remove(tag);
            this.AllOfTags.Remove(tag);
            this.AnyOfTags.Remove(tag);
            this.ExcludeTags.Remove(tag);
            return true;
        } else {
            return false;
        }
    }

    public ImageOrigin FocusedImage {
        get { return focusedImage; }
        set {
            if (value != focusedImage) {
                focusedImage = value;
                NotifyPropertyChanged("FocusedImage");
            }
        }
    }

    public ImageOrigin[] CompleteSet {
        get { return completeSet; }
    }

    public void SetCompleteSet(ImageOrigin[] completeSet, ImageOrigin focusedImage) {
        this.completeSet = completeSet;
        this.displaySet = completeSet;
        this.focusedImage = focusedImage;
        loader.SetImageOrigins(this.DisplaySet, focusedImage);
        AssertInvariant();
        NotifyPropertyChanged("");
        this.changesToSave = false;
    }

    private void AssertInvariant() {
        Debug.Assert(completeSet != null);
        Debug.Assert(focusedImage == null || completeSet.Contains(focusedImage));
        Debug.Assert(focusedImage == null || displaySet.Contains(focusedImage));
    }


    public ImageOrigin[] DisplaySet {
        get { return displaySet; }
        set {
            displaySet = value;
            if (!displaySet.Contains(focusedImage))
                focusedImage = null;
            AssertInvariant();
            NotifyPropertyChanged("DisplaySet");
        }
    }

    public void SelectDirectories(bool firstTime) {
        fileListSource.SelectDirectoriesForTriage(firstTime,
            (SelectDirectoriesCompletedEventArgs args) => {
                this.SetCompleteSet(args.imageOrigins, args.initialFocus);
                this.focusedImage = args.initialFocus;
            }
            );
    }

    public void OnSlideshowExit(SlideShow ss) {
        PopScreen(ss.TypeaheadImage);

        //grid.Children.Remove(ss);
        //ss.Unload();
        //loader.Mode = LoaderMode.Thumbnail;
        //photoGrid.Focus();
        //photoGrid.MoveFocus(ss.TypeaheadImage);
    }

    protected void NotifyPropertyChanged(String info) {
        if (PropertyChanged != null) {
            var args = new PropertyChangedEventArgs(info);
            PropertyChanged(this, args);
        }
    }

    private void CreateCommands() {
        Command command;

        command = new Command();
        command.Text = "Restore";
        command.HasMenuItem = false;
        command.Button = restoreButton;
        command.Execute += delegate () {
            ToggleFullScreen();// WindowState = WindowState.Normal;
        };
        commands.AddCommand(command);

#if WPF
        command = new Command();
        command.Text = "Quit";
        command.Button = closeButton;
        command.HasMenuItem = false;
        //            if (App.EnableEscapeKey) {
        command.Key = Key.Escape;
        //            }
        command.Execute += delegate () {
            //ExitAppMaybe();
            PopScreen();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Minimize";
        command.HasMenuItem = false;
        command.Button = minimizeButton;
        command.Execute += delegate () {
            window.WindowState = WindowState.Minimized;
        };
        commands.AddCommand(command);
#endif
        // does this cmd still make sense?
        command = new Command();
        command.Text = "Open folder";
        command.Key = Key.O;
        command.ModifierKeys = ModifierKeys.Control;
        // command.Button = openFolderButton;
        command.Execute += delegate () {
            SelectDirectories(false/* first time*/);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.T;
        command.Text = "Triage new photos into Good";
        command.Execute += delegate () {
            // copy into Good- folders, but not into database
            fileListSource.SelectDirectoriesForTriage(false /* not 1st time */,
                (SelectDirectoriesCompletedEventArgs args) => {
                    this.SetCompleteSet(args.imageOrigins, args.initialFocus);
                }
                 );
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.I;
        command.Text = "Import photos to database";
        command.Execute += delegate () {
            fileListSource.SelectOneDirectory(
                (SelectDirectoriesCompletedEventArgs args) => {
                    // add to database
                    var newSet = this.CompleteSet.Concat(args.imageOrigins).ToArray();
                    this.SetCompleteSet(newSet, args.initialFocus);
                    this.focusedImage = args.imageOrigins.FirstOrDefault();
                }
                 );
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.I;
        command.ModifierKeys = ModifierKeys.Shift;
        command.Text = "Import selection to database";
        command.Execute += delegate () {
            fileListSource.SelectOneDirectory(
                (SelectDirectoriesCompletedEventArgs args) => {
                    var set = args.imageOrigins.ToLookup(i => System.IO.Path.GetFileName(i.SourcePath));
                    foreach (var i in this.CompleteSet) {
                        if (set.Contains(System.IO.Path.GetFileName(i.SourcePath)))
                            i.IsSelected = true;
                    }
                }
                 );
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Scan for good photos to import";
        command.Execute += delegate () {
            // import new good/better/best
            IEnumerable<ImageOrigin> addedOrigins = ImportGoodBetterBest(this.CompleteSet, this.Tags);
            AutoTagDatesAndPlaces(addedOrigins, this.Tags);
            List<ImageOrigin> newSet = this.CompleteSet.Concat(addedOrigins).ToList();
            newSet.Sort(new ImageOrigin.OriginComparer());
            this.SetCompleteSet(newSet.ToArray(), newSet.First());
            this.focusedImage = addedOrigins.FirstOrDefault();

            CopyMatchingRawFilesIfAvailable(addedOrigins);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Copy matching RAW files";
        command.Execute += delegate () {
            CopyMatchingRawFilesIfAvailable(this.CompleteSet);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Copy (import) photos from external source";
        command.Key = Key.E;
        command.ModifierKeys = ModifierKeys.Shift;
        command.Execute += delegate () {
            PhotoImporter.ImportPhotos();
        };
        commands.AddCommand(command);

        commands.AddMenuSeparator();

        command = new Command();
        command.Text = "Show selected files only";
        command.Key = Key.S;
        command.Execute += delegate () {
            if (this.DisplaySet == this.CompleteSet) {
                var newDisplaySet = this.DisplaySet.Where((i) => i.IsSelected).ToArray();
                this.DisplaySet = newDisplaySet;
            } else {
                this.DisplaySet = this.CompleteSet;
            }
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.W;
        command.Text = "Save database (write)";
        command.Execute += delegate () {
            WriteDatabase();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Export tags";
        command.Execute += delegate () {
            ExportTagsToLightroom();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.F5;
        command.Text = "Update filters";
        command.Execute += delegate () {
            UpdateFilters();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.P;
        command.Text = "Page Designer";
        command.Execute += delegate () {
            PushScreen(new PageDesigner());
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Full-screen mode";
        command.Key = Key.F11;
        command.Execute += delegate () {
            ToggleFullScreen();
        };
        commands.AddCommand(command);

        commands.AddMenuSeparator();

        command = new Command();
        command.Text = "Help...";
        command.Key = Key.F1;
        command.Execute += delegate () {
            fileListSource.ShowHelp();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.OemQuestion;
        command.WithOrWithoutShift = true;
        command.DisplayKey = "?";
        command.Text = "Show keyboard shortcuts";
        command.Execute += delegate () {
            ShowKeyboardShortcuts();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Key = Key.F7;
        command.Text = "GC";
        command.Execute += delegate () {
            GC.Collect();
            GC.Collect(2);
            GC.Collect(0);
            GC.WaitForPendingFinalizers();
            GC.Collect(2);
            GC.Collect(0);
        };
        commands.AddCommand(command);

        //command = new Command();
        //command.Key = Key.F7;
        //command.Text = "GC";
        //command.Execute += delegate() {
        //Console.WriteLine("path,filename,focalLength,iso,fstop,exposureTime.numerator,exposureTime.denominator");

        //foreach (var d in Directory.GetDirectories(@"D:\Good Pictures")) {
        //    if (Path.GetFileName(d).StartsWith("good")) {
        //        foreach (var f in Directory.GetFiles(d, "*.jpg")) {
        //            try {
        //                var io = new ImageOrigin(f, null, 0);
        //                var ii = ImageDecoder.Decode(io, 0, 0);
        //                if (ii.IsValid) {
        //                    var md =
        //                    new string[] { f, 
        //                    Path.GetFileNameWithoutExtension(f),
        //                ii.focalLength.ToString(), 
        //                ii.isospeed.numerator.ToString(), 
        //                ((float) ii.fstop.numerator / ii.fstop.denominator).ToString(),
        //                ii.exposureTime.numerator.ToString(),
        //                ii.exposureTime.denominator.ToString(),
        //            };
        //                    Console.WriteLine(string.Join(",", md));
        //                }
        //            } catch (OverflowException) {
        //            }
        //        }
        //    }
        //}
        //};
        //commands.AddCommand(command);  

        command = new Command();
        command.Key = Key.F8;
        command.Text = "Show Process ID";
        command.Execute += delegate () {
            string pid = Process.GetCurrentProcess().Id.ToString();
            ThemedMessageBox.Show("ProcessID = " + pid);
            Clipboard.SetText(pid);
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "About...";
        command.Execute += delegate () {
            new AboutDialog().ShowDialog();
        };
        commands.AddCommand(command);

        command = new Command();
        command.Text = "Show all dialogs (debug)";
        command.Key = Key.F12;
        command.Execute += delegate () {
            ShowAllDialogs();
        };
        commands.AddCommand(command);

    }

    private void WriteDatabase() {
        var t = DateTimeOffset.Now;
        string time = string.Format("{0:D4}-{1:D2}-{2:D2}--{3:D2}-{4:D2}-{5:D2}-{6:D3}", t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, t.Millisecond);
        string[] mainLines = ImageOrigin.Persist(this.CompleteSet);
        string[] tagsLines = PhotoTag.Persist(this.Tags);
        File.WriteAllLines(dbDir + @"\" + time + ".csv", mainLines);
        File.WriteAllLines(dbDir + @"\" + time + "_tag_defs.csv", tagsLines);
        File.WriteAllLines(dbDirCopy + @"\photos.csv", mainLines);
        File.WriteAllLines(dbDirCopy + @"\tags.csv", tagsLines);
        changesToSave = false;
    }

    private void ExportTagsToLightroom() {
        string[] tagsLines = PhotoTag.PersistToLightroomFormat(this.Tags);
        File.WriteAllLines(@"C:\Users\Nick\Downloads\Folio-tags.txt", tagsLines);
    }

    private void ToggleFullScreen() {
#if WPF
        if (IsFullScreen) {
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.ResizeMode = ResizeMode.CanResize;
            this.windowControls1.Visibility = Visibility.Collapsed;
        } else {
            window.WindowStyle = WindowStyle.None;
            window.WindowState = WindowState.Maximized;
            window.ResizeMode = ResizeMode.NoResize;
            this.windowControls1.Visibility = Visibility.Visible;
        }
#else
        Application.Current.Host.Content.IsFullScreen = !Application.Current.Host.Content.IsFullScreen;
#endif
    }

    public bool IsFullScreen {
        get {
#if WPF
            return window.WindowStyle == WindowStyle.None;
#else
             return Application.Current.Host.Content.IsFullScreen;
#endif
        }
    }

    private List<IScreen> screenStack = new List<IScreen>();

    public void PushScreen(IScreen iscreen) {
        var screen = (FrameworkElement)iscreen;
        screenStack.Add(iscreen);
        SetScreen(iscreen, null);
        //iscreen.Activate();
    }

    private void SetScreen(IScreen iscreen, ImageOrigin focus) {
        var screen = (FrameworkElement)iscreen;
        screenHolder.Child = screen;
        screen.Focus();
        iscreen.Activate(focus);
    }

    public void PopScreen() {
        PopScreen(null);
    }

    public void PopScreen(ImageOrigin focus) {
        Debug.Assert(screenStack.Count > 0);
        Debug.Assert(screenHolder.Child != null);

        if (screenStack.Count == 1) {
            ExitAppMaybe();
            return;
        } else {
            Debug.Assert(screenStack.Count >= 2);
            IScreen iscreen = screenStack.Last();
            FrameworkElement screen = (FrameworkElement)iscreen;
            iscreen.Deactivate();
            screenStack.RemoveAt(screenStack.Count - 1);

            iscreen = screenStack.Last(); // new last
            screen = (FrameworkElement)iscreen; // new last
            SetScreen(iscreen, focus);
        }
    }

    public IScreen TopScreen {
        get {
            return screenStack.Last();
        }
    }

    private void ExitAppMaybe() {
        if (changesToSave) {
            var result = ThemedMessageBox.Show("Save changes?", "", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes) {
                WriteDatabase();
            }
        }
        window.Close();
    }

    public void UpdateCache() {
        loader.UpdateWorkItems();
    }

    private void ShowKeyboardShortcuts() {
        var sections = new List<Shell.ShortcutSection>();

        // Get current screen shortcuts
        var currentScreen = TopScreen as FrameworkElement;
        string currentScreenName = GetScreenName(currentScreen);

        if (currentScreen != null) {
            var screenCommands = GetCommandsFromScreen(currentScreen);
            if (screenCommands.Count > 0) {
                sections.Add(new Shell.ShortcutSection {
                    SectionName = currentScreenName,
                    Commands = screenCommands
                });
            }
        }

        // Add root/global shortcuts
        var rootCommands = new List<Shell.ShortcutCommand>();
        foreach (var cmd in commands.GetCommands()) {
            if (cmd.HasMenuItem) {
                string keyText = CommandHelper.GetKeyText(cmd);
                if (keyText != null) {
                    rootCommands.Add(new Shell.ShortcutCommand {
                        KeyText = keyText,
                        Description = cmd.Text
                    });
                }
            }
        }

        if (rootCommands.Count > 0) {
            sections.Add(new Shell.ShortcutSection {
                SectionName = "Global",
                Commands = rootCommands
            });
        }

        var window = new KeyboardShortcutsDialog(sections);
        window.ShowDialog();
    }

    private string GetScreenName(FrameworkElement screen) {
        if (screen == null) return "Unknown";

        var typeName = screen.GetType().Name;
        switch (typeName) {
            case "PhotoGrid":
                return "Library/Grid";
            case "SlideShow":
                return "Slideshow";
            case "PageDesigner":
                return "Page Designer";
            case "BookViewerFullscreen":
                return "Page Designer Fullscreen";
            default:
                return typeName;
        }
    }

    private List<Shell.ShortcutCommand> GetCommandsFromScreen(FrameworkElement screen) {
        var result = new List<Shell.ShortcutCommand>();

        // Use reflection to get the commands field
        var type = screen.GetType();
        var commandsField = type.GetField("commands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (commandsField != null) {
            var commandHelper = commandsField.GetValue(screen) as CommandHelper;
            if (commandHelper != null) {
                foreach (var cmd in commandHelper.GetCommands()) {
                    if (cmd.HasMenuItem) {
                        string keyText = CommandHelper.GetKeyText(cmd);
                        if (keyText != null) {
                            result.Add(new Shell.ShortcutCommand {
                                KeyText = keyText,
                                Description = cmd.Text
                            });
                        }
                    }
                }
            }
        }

        return result;
    }

    private List<Window> openDebugDialogs = new List<Window>();

    private void ShowAllDialogs() {
        // Close any previously opened debug dialogs
        foreach (var dialog in openDebugDialogs) {
            try {
                dialog.Close();
            } catch { }
        }
        openDebugDialogs.Clear();

        // Helper method to add and position a dialog
        void AddDialog(Window dialog, int x, int y) {
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = x;
            dialog.Top = y;

            // Handle ESC key to close all dialogs
            dialog.KeyDown += (sender, e) => {
                if (e.Key == Key.Escape) {
                    CloseAllDebugDialogs();
                    e.Handled = true;
                }
            };

            // Track when dialogs are closed
            dialog.Closed += (sender, e) => {
                openDebugDialogs.Remove((Window)sender);
            };

            openDebugDialogs.Add(dialog);
            dialog.Show();
        }

        // Row 1: QuestionWindow, ImportPhotosDialog, ImportProgressDialog
        var questionWindow = new Utilities.QuestionWindow();
        questionWindow.DialogTitle = "Sample Question?";
        questionWindow.Result = "Sample Answer";
        AddDialog(questionWindow, 50, 50);

        AddDialog(new Importer.ImportPhotosDialog(SdCardRoot), 450, 50);

        var progressDialog = new Importer.ImportProgressDialog();
        progressDialog.UpdateProgress(5, 10, "sample_photo.jpg");
        AddDialog(progressDialog, 950, 50);

        AddDialog(new AboutDialog(), 1500, 50);

        // Row 2: SelectFolders, SelectFolder2
        AddDialog(new Utilities.SelectFolders(this.fileListSource), 50, 400);
        AddDialog(new Utilities.SelectFolder2(this.fileListSource), 950, 400);

        var sections = new List<Shell.ShortcutSection>();
        sections.Add(new Shell.ShortcutSection {
            SectionName = "Sample",
            Commands = new List<Shell.ShortcutCommand> {
                new Shell.ShortcutCommand { KeyText = "F1", Description = "Sample command" }
            }
        });

        AddDialog(new KeyboardShortcutsDialog(sections), 1600, 400);
        IsShowingAllDialogs = true;
    }

    public static bool IsShowingAllDialogs = false;

    private void CloseAllDebugDialogs() {
        foreach (var dialog in openDebugDialogs.ToList()) {
            try {
                dialog.Close();
            } catch { }
        }
        openDebugDialogs.Clear();
        IsShowingAllDialogs = false;
    }
}
