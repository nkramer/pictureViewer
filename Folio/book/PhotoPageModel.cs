using Folio.Core;
using Folio.Utilities;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace Folio.Book;
public class PhotoPageModel : ChangeableObject {
    private string templateName = "875x1125_32_1p1h0v1t";

    private ObservableCollection<ImageOrigin?> images = new ObservableCollection<ImageOrigin?>();
    private string richText = ""; // xaml
    private string richText2 = ""; // xaml
    private bool flipped = false;
    private bool showGridLines = true;
    private BookModel? book = null;
    private string backgroundColor = "#FFFFFFFF";  // white
    private string foregroundColor = "#FF000000";  // black
    private bool errorState = false;

    public PhotoPageModel(BookModel? book) {
        this.book = book;
        this.images.CollectionChanged += new NotifyCollectionChangedEventHandler(images_CollectionChanged);
    }

    private void images_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        // Reset error state when images change, as the new layout might work
        ErrorState = false;
        Debug.WriteLine("pageModel.ErrorState = false;");
        if (book != null)
            book.OnImagesChanged();
    }

    public string TemplateName {
        get { return templateName; }
        set { templateName = value; NotifyPropertyChanged("TemplateName"); }
    }

    public string TemplateVersion {
        get {
            return PhotoPageView.TemplateVersion(TemplateName);
        }
    }

    public bool Flipped {
        get { return flipped; }
        set { flipped = value; NotifyPropertyChanged("Flipped"); }
    }

    public string RichText {
        get { return richText; }
        set { richText = value; NotifyPropertyChanged("RichText"); }
    }

    // hackorama
    // Text for a 2nd column
    public string RichText2 {
        get { return richText2; }
        set {
            richText2 = value; NotifyPropertyChanged("RichText");
            // hack: yup, propchange for the wrong prop!
        }
    }

    public bool ShowGridLines {
        get { return showGridLines; }
        set { showGridLines = value; NotifyPropertyChanged("ShowGridLines"); }
    }

    // always in caps, always w/ alpha
    public string BackgroundColor {
        get { return backgroundColor; }
        set { backgroundColor = value; NotifyPropertyChanged("BackgroundColor"); }
    }

    // always in caps, always w/ alpha
    public string ForegroundColor {
        get { return foregroundColor; }
        set { foregroundColor = value; NotifyPropertyChanged("ForegroundColor"); }
    }

    public bool ErrorState {
        get { return errorState; }
        set { errorState = value; NotifyPropertyChanged("ErrorState"); }
    }

    public void AddImage(ImageOrigin? i) {
        this.Images.Add(i);
    }

    public ObservableCollection<ImageOrigin?> Images {
        get { return images; }
        private set {
            images = value;
            // important this is the 1st listener
            images.CollectionChanged += new NotifyCollectionChangedEventHandler(images_CollectionChanged);
        }
    }

    public static PhotoPageModel Parse(XElement e, ILookup<string, ImageOrigin> originLookup, BookModel book) {
        Debug.Assert(e.Name.LocalName == "PhotoPageModel");
        var m = new PhotoPageModel(book);

        if (e.Attribute("Flipped") != null)
            m.Flipped = (e.Attribute("Flipped")!.Value.ToLower() == "true");
        if (e.Attribute("TemplateName") != null)
            m.TemplateName = e.Attribute("TemplateName")!.Value;
        if (e.Attribute("BackgroundColor") != null)
            m.BackgroundColor = e.Attribute("BackgroundColor")!.Value;
        if (e.Attribute("ForegroundColor") != null)
            m.ForegroundColor = e.Attribute("ForegroundColor")!.Value;

        var origins = e.Elements("ImageOrigin").Select(elt => {
            string imageName = elt.Attribute("Name")!.Value;
            if (elt.Attribute("Name")!.Value == "")
                return null;
            //var matches = RootControl.Instance.CompleteSet.Where(i => i.SourcePath == imageName);
            var matches = originLookup[imageName];
            Debug.Assert(matches.Count() == 1, $"Can't find file {imageName}");
            return matches.First();
        });
        m.Images = new ObservableCollection<ImageOrigin?>(origins);
        var richtext = e.Element("RichText");
        m.RichText = richtext!.Value;
        var richtext2 = e.Element("RichText2");
        if (richtext2 != null) {
            m.RichText2 = richtext2.Value;
        }
        return m;
    }

    public XElement Persist() {
        var e = new XElement("PhotoPageModel",
            new XAttribute("TemplateName", this.TemplateName),
            new XAttribute("Flipped", this.Flipped),
            new XAttribute("BackgroundColor", this.BackgroundColor),
            new XAttribute("ForegroundColor", this.ForegroundColor),

            images.Select(i => new XElement("ImageOrigin",
                new XAttribute("Name", (i == null) ? "" : i.SourcePath)
                )));
        e.Add(new XElement("RichText", this.RichText));
        if (this.RichText2 != null && this.RichText2 != "") {
            e.Add(new XElement("RichText2", this.RichText2));
        }
        return e;
    }

    public PhotoPageModel Clone() {
        var m = new PhotoPageModel(this.book);
        m.templateName = this.templateName;
        m.flipped = this.flipped;
        m.backgroundColor = this.backgroundColor;
        m.foregroundColor = this.foregroundColor;

        m.images = this.images; // shallow copy for perf
        // it's ok the wrong colchanged event handler is hooked up -- gets to the same place in the end

        m.richText = this.richText;
        m.richText2 = this.richText2;
        m.book = this.book;
        return m;
        //XElement xml = this.Persist();
        //var clone = Parse(xml);
        //// don't clone event listeners!
        //return clone;
    }
}
