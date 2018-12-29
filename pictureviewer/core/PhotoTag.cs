using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Pictureviewer.Utilities;

namespace Pictureviewer.Core
{
    public class PhotoTag : ChangeableObject
    {
        private string name;
        private PhotoTag parent;
        private ObservableCollection<PhotoTag> children = new ObservableCollection<PhotoTag>();

        //private Tag(string line, int constructorDisambiguator)
        public PhotoTag(string name, PhotoTag parent) {
            this.Name = name;
            this.Parent = parent;
        }

        public string Name
        {
            get { return name; }
            set { name = value; NotifyPropertyChanged("Name");  }
        }

        public PhotoTag Parent
        {
            get { return parent; }
            set {
                Debug.Assert(this != value);
                
                if (parent != null) {
                    parent.Children.Remove(this);
                }

                parent = value;
                
                if (parent != null) {
                    //parent.Children.Add(this);
                    PhotoTag next = parent.Children.FirstOrDefault(t => string.Compare(t.Name, this.Name) > 0);
                    if (next == null)
                        parent.Children.Add(this);
                    else {
                        int index = parent.Children.IndexOf(next); //(next == null) ? 0 : parent.Children.IndexOf(next);
                        parent.Children.Insert(index, this);
                    }
                }

                NotifyPropertyChanged("Parent"); 
            }
        }

        public ObservableCollection<PhotoTag> Children
        {
            get { return children; }
        }

        public string QualifiedName
        {
            get
            {
                if (Parent == null)
                    return Name;
                else
                    return Parent.QualifiedName + "|"+Name;
            }
        }

        public bool IsPrefixOf(PhotoTag tag)
        {
            return this == tag
                || (this.QualifiedName.Length < tag.QualifiedName.Length
                && tag.QualifiedName.StartsWith(this.QualifiedName)
                && tag.QualifiedName[this.QualifiedName.Length] == '|');
        }

        public static bool Matches(PhotoTag elt, IEnumerable<PhotoTag> collection)
        {
          return collection.Any(t => elt.IsPrefixOf(t));
        }

        public static PhotoTag FindOrMake(string qualifiedName, IEnumerable<PhotoTag> rootTags)
        {
            String[] pieces = qualifiedName.Split('|');
            Debug.Assert(pieces.Length >= 2);
            PhotoTag tag = rootTags.FirstOrDefault(t => t.Name == pieces[0]);
            Debug.Assert(tag != null);
            pieces = pieces.Skip(1).ToArray();
            foreach (var p in pieces) {
                PhotoTag next = tag.Children.Where(t => t.Name == p).FirstOrDefault();
                if (next == null) {
                    next = new PhotoTag(p, tag);
                }
                tag = next;
            }
            return tag;
        }

        public static ObservableCollection<PhotoTag> Parse(string[] lines, out Dictionary<string, PhotoTag> tagLookup)
        {
            String[][] dataLines = lines.Skip(1)
                .Select(x => x.Split(new string[] { "," }, StringSplitOptions.None))
                .OrderBy(x => x[3] + "|" + x[0])
                .ToArray();
            var lookup = new Dictionary<string, PhotoTag>();
            var tags = new ObservableCollection<PhotoTag>();
            foreach (var line in dataLines) {
                String qualifiedParentName =line[3];
                var parent = lookup.ContainsKey(qualifiedParentName) ? lookup[qualifiedParentName] : null;
                var tag = new PhotoTag(line[0], parent);
                lookup[tag.QualifiedName] = tag;
                tags.Add(tag);
            }
            var result = new ObservableCollection<PhotoTag>(tags.Where(t => t.Parent == null));
            tagLookup = lookup;
            return result;
        }

        private static void RecursiveFlatten(PhotoTag tag, ObservableCollection<PhotoTag> results) {
            // seems like there should be a LINQ method for flattening the hierarchy, but I can't find one...
            results.Add(tag);
                foreach (var t in tag.Children) {
                    RecursiveFlatten(t, results);
                }
       }

        public static string[] Persist(IEnumerable<PhotoTag> tags) {
            // seems like there should be a LINQ method for flattening the hierarchy, but I can't find one...
            var flattened = new ObservableCollection<PhotoTag>();
            foreach (var t in tags) {
                RecursiveFlatten(t, flattened);
            }

            var lines = new string[] { "Tag,Qualified Tag,Parent Tag,Qualified Parent Tag,GPS Longitude,GPS Latitude,Notes" }
                .Concat(flattened.Select(t => string.Format("{0},,,{1}", t.Name, t.Parent == null ? "" : t.Parent.QualifiedName)));
            var result = lines.ToArray();
            return result;
        }

        private static void RecursiveFlattenForLightroom(PhotoTag tag, List<string> results, int indentLevel) {
            // seems like there should be a LINQ method for flattening the hierarchy, but I can't find one...
            string prefix = "";
            for (int i = 0; i < indentLevel; i++)
                prefix += "\t";
            results.Add(prefix + tag.Name);

            foreach (var t in tag.Children) {
                RecursiveFlattenForLightroom(t, results, indentLevel + 1);
            }
        }

        public static string[] PersistToLightroomFormat(IEnumerable<PhotoTag> tags, int indentLevel = 0) {
            // seems like there should be a LINQ method for flattening the hierarchy, but I can't find one...
            var results = new List<string>();
            foreach (var t in tags) {
                RecursiveFlattenForLightroom(t, results, 0);
            }
            return results.ToArray();
        }


        public override string ToString()
        {
            return QualifiedName;
        }

        public static string GetRatingString(int ratingNum)
        {
            string rating = null;
            switch (ratingNum) {
                case 0: rating = null; break;
                case 1: rating = "Rated|*"; break;
                case 2: rating = "Rated|**"; break;
                case 3: rating = "Rated|***"; break;
                case 4: rating = "Rated|****"; break;
                case 5: rating = "Rated|*****"; break;
                default: throw new Exception("unknown rating");
            }
            return rating;
        }
    }
}
