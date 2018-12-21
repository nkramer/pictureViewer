using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Xml.Linq;
using System.Collections.Specialized;

namespace pictureviewer {
    public class BookModel : ChangeableObject {
        private PhotoPageModel selectedPage;
        private ObservableCollection<PhotoPageModel> pages = new ObservableCollection<PhotoPageModel>();
        private List<TwoPages> twoPages = null;

        public BookModel() {
            pages.CollectionChanged += new NotifyCollectionChangedEventHandler(pages_CollectionChanged);
        }

        private void pages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            NotifyPropertyChanged("TwoPages");
            if (twoPages != null) {
                twoPages = null;
            }
        }

        // added or removed
        // exists to get event order right -- before Images.CollectionChanged
        public event EventHandler ImagesChanged;

        // called by PhotoPageModel
        internal void OnImagesChanged() {
            if (ImagesChanged != null)
                ImagesChanged(this, EventArgs.Empty);
        }

        public PhotoPageModel SelectedPage {
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
                    c.Add(new TwoPages(null, pages[0]));
                    for (int i = 1; i < pages.Count; i = i + 2) {
                        var t = new TwoPages(pages[i], (i + 1 < pages.Count) ? pages[i + 1] : null);
                        c.Add(t);
                    }
                    twoPages = c;
                }
                return twoPages;
            }
        }

        public void Parse() {
            //RootControl.Instance.CompleteSet.Where(i => i.SourcePath == eltName);
            ILookup<string, ImageOrigin> originLookup = RootControl.Instance.CompleteSet.ToLookup(i => i.SourcePath);

            var doc = XDocument.Load(RootControl.dbDir + @"\testPhotoBook.xml");
            Debug.Assert(doc.Root.Name.LocalName == "PhotoBook");
            var pages = doc.Root.Elements("PhotoPageModel").Select(e => PhotoPageModel.Parse(e, originLookup, this));

            foreach (var m in pages) {
                this.pages.Add(m);
            }
            //this.models = new ObservableCollection<PhotoPageModel>(models);
        }
    }
}
