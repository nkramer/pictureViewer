using Folio.Core;
using Folio.Shell;
using Folio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Folio.Book {
    // The data for a photo book -- ie, layouts and images for each page.
    public class BookModel : ChangeableObject {
        private PhotoPageModel? selectedPage;
        private ObservableCollection<PhotoPageModel> pages = new ObservableCollection<PhotoPageModel>();
        private List<TwoPages>? twoPages = null;

        public BookModel() {
            pages.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) => {
                NotifyPropertyChanged("TwoPages");
                if (twoPages != null) {
                    twoPages = null;
                }
            };
        }

        // added or removed
        // exists to get event order right -- before Images.CollectionChanged
        public event EventHandler? ImagesChanged;

        // called by PhotoPageModel
        internal void OnImagesChanged() {
            if (ImagesChanged != null)
                ImagesChanged(this, EventArgs.Empty);
        }

        public PhotoPageModel? SelectedPage {
            get { return selectedPage; }
            set {
                selectedPage = value;
                NotifyPropertyChanged("SelectedPage");
                NotifyPropertyChanged("SelectedTwoPage");
            }
        }

        public TwoPages TwoPageFromPage(PhotoPageModel page) {
            //if (twoPages == null) return null;
            Debug.Assert(twoPages != null);
            var result = twoPages.FirstOrDefault(t => t.Left == page || t.Right == page);
            return result;
        }

        public TwoPages SelectedTwoPage {
            get {
                if (twoPages == null) return null;
                return TwoPageFromPage(SelectedPage);
            }
            //get { return selectedPage; }
            //set { selectedPage = value; NotifyPropertyChanged("SelectedPage"); }
        }

        public ObservableCollection<PhotoPageModel> Pages {
            get { return pages; }
        }

        // regenerated from scratch when underlying collection changes
        public List<TwoPages> TwoPages {
            get {
                if (twoPages == null) {
                    var c = new List<TwoPages>();
                    Debug.Assert(pages[0] != null);
                    // hack - First page should really be a blank page or a non page. 
                    // If we just make it null though, you get the right behavior in the table of
                    // contents, but the wrong behavior in the main display. This is because
                    // PhotoPageView.PageChanged doesn't know what to do when the page is null. 
                    // to do: Create a blank page template.
                    var dummyPage = new PhotoPageModel(this);
                    dummyPage.BackgroundColor = "#00000000";
                    c.Add(new TwoPages(dummyPage, pages[0]));
                    for (int i = 1; i < pages.Count; i = i + 2) {
                        var t = new TwoPages(pages[i], (i + 1 < pages.Count) ? pages[i + 1] : null);
                        c.Add(t);
                    }
                    twoPages = c;
                }
                return twoPages;
            }
        }

        // Parses an XML string and returns a BookModel
        public static BookModel Parse(string xmlString) {
            ILookup<string, ImageOrigin> originLookup = RootControl.Instance.CompleteSet.ToLookup(i => i.SourcePath);

            var doc = XDocument.Parse(xmlString);
            Debug.Assert(doc.Root.Name.LocalName == "PhotoBook");

            var bookModel = new BookModel();
            var pages = doc.Root.Elements("PhotoPageModel").Select(e => PhotoPageModel.Parse(e, originLookup, bookModel));

            foreach (var m in pages) {
                bookModel.pages.Add(m);
            }

            return bookModel;
        }

        // Loads a BookModel from a file (implemented in terms of Parse)
        public static BookModel Load(string filePath) {
            var xmlString = File.ReadAllText(filePath);
            return Parse(xmlString);
        }

        // Serializes the book to an XML string
        public string Serialize() {
            var doc = new XDocument(new XElement("PhotoBook",
                pages.Select(m => m.Persist())));
            return doc.ToString();
        }

        // Saves the book to a file
        public void Save(string filePath) {
            var xmlString = Serialize();
            File.WriteAllText(filePath, xmlString);
        }
    }
}
