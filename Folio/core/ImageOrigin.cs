using Folio.Shell;
using Folio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Folio.Core {
    // in retrospect, this class may have been overkill -- might have been easier to just 
    // consistently pass a string around representing the source path
    public class ImageOrigin : ChangeableObject {
        private ObservableCollection<PhotoTag> tags;// = new ObservableCollection<PhotoTag>();
        private string sourcePath;
        private string? targetPath;
        private bool isSelected = false;
        private double rotation = 0;
        private bool flip = false;

        public static IEnumerable<ImageOrigin> Parse(string[] lines, Dictionary<string, PhotoTag> tagLookup) {
            // file format comes from psedb tool, which exports Adobe Elements databases to a csv format
            // Filename,Id,Volume,Volume Serial Number,Status,Type,Date,Caption,Notes,Rating,Hidden,Tags, Qualified Tags,Albums,Qualified Albums,Album Positions,Stack Top,Stack Position,Version Set Top,Version Set Position,db:guid,EXIF:Date,xmp:CreateDate,pse:FileNameOriginal,pse:FileDateOriginal,pse:FileSizeOriginal,tiff:ImageWidth,tiff:ImageHeight,pse:FileDate,pse:FileSize,pse:guid,exif:Make,exif:Model,exif:FNumber,exif:ExposureTime,exif:ExposureBias,exif:ExposureProgram,exif:FocalLength,exif:Flash,exif:ISOSpeedRatings,xmpDM:Duration,exif:GPSLongitude,exif:GPSLatitude,dc:creator,exif:RelatedSoundFile,pse:ProxyFile,dc:title,pse:LastOrganizerModifiedTime,pre:FrameRate,pre:MediaDuration,pre:MediaFormat,pre:Resolution

            String[][] dataLines = lines.Skip(1)
                .Where(x => !x.Contains(".mp3"))
                .Select(x => x.Split(new string[] { "," }, StringSplitOptions.None))
                .ToArray();
            var origins = dataLines.Select(d => {
                //var ratingString = d[9];
                int ratingNum = (d[9] == "") ? 0 : int.Parse(d[9]);
                var tagsString = d[12];
                var tagStrings = tagsString.Split(new string[] { "^" }, StringSplitOptions.RemoveEmptyEntries);
                Debug.Assert(tagStrings.All(s => tagLookup.ContainsKey(s)));
                var tags = tagStrings.Select(s => tagLookup[s]);
                var tags2 = new ObservableCollection<PhotoTag>(tags);
                string ratingStr = PhotoTag.GetRatingString(ratingNum);
                PhotoTag rating = (ratingStr == null) ? null : tagLookup[ratingStr];
                if (rating != null)
                    tags2.Add(rating);
                var image = new ImageOrigin(d[0], null, tags2);
                if (d.Length > 52)
                    image.Rotation = double.Parse(d[52]);
                if (d.Length > 53)
                    image.Flip = bool.Parse(d[53]);
                return image;
            });
            //origins.Sort(new OriginComparer());
            // OrderBy is N^2, so use List.Sort instead

            //        origins.OrderBy(o=>o.SourcePath, new IComparer<string> {
            //             public int Compare(string left, string right) {
            //                 return CompareFilename(left, right);
            //             }
            //});

            //return new ObservableCollection<ImageOrigin>(origins);
            return origins;
        }

        internal class OriginComparer : Comparer<ImageOrigin> {
            private FileComparer fc = new FileComparer();
            public override int Compare(ImageOrigin? x, ImageOrigin? y) {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return fc.Compare(x.SourcePath, y.SourcePath);
            }
        }


        internal class FileComparer : Comparer<string> {
            private static System.Globalization.CompareInfo globCompare = System.Globalization.CompareInfo.GetCompareInfo("en-us");

            public override int Compare(string? left, string? right) {
                if (left == null && right == null) return 0;
                if (left == null) return -1;
                if (right == null) return 1;
                //var res1 = CompareFilename(left, right);
                var res2 = CompareFilename2(left, right);
                //Debug.Assert(res1 == res2);
                return res2;
            }

            private static void GetFilenameWithoutNumber2(string filename, out int filenameStart, out int coreFilenameEnd, out int fileNumberLength) {
                filenameStart = filename.LastIndexOf('\\') + 1;
                int fileExtensionStart = filename.LastIndexOf('.');

                int i;
                for (i = fileExtensionStart - 1; i >= filenameStart; i--) {
                    if (!char.IsDigit(filename[i])) break;
                }
                coreFilenameEnd = i + 1;
                fileNumberLength = fileExtensionStart - coreFilenameEnd;
            }

            private static int GetFileNumber(string filename, int coreFilenameEnd, int fileNumberLength) {
                string numberStr = filename.Substring(coreFilenameEnd, fileNumberLength);
                int fileNumber;
                if (numberStr.Length == 0)
                    fileNumber = -1;
                else
                    fileNumber = int.Parse(numberStr);
                return fileNumber;
            }

            private static int CompareFilename2(string left, string right) {
                int leftFilenameStart;
                int leftCoreFilenameEnd;
                int leftFileNumberLength;
                GetFilenameWithoutNumber2(left, out leftFilenameStart, out leftCoreFilenameEnd, out leftFileNumberLength);
                //var d_leftcore = left.Substring(leftFilenameStart, leftCoreFilenameEnd - leftFilenameStart);

                int rightFilenameStart;
                int rightCoreFilenameEnd;
                int rightFileNumberLength;
                GetFilenameWithoutNumber2(right, out rightFilenameStart, out rightCoreFilenameEnd, out rightFileNumberLength);
                //var d_rightcore = right.Substring(rightFilenameStart, rightCoreFilenameEnd - rightFilenameStart);

                //var d_comparedString = "";
                //var d_ll = left.Length;
                //var d_rl = right.Length;
                //if (rightFilenameStart + leftCoreFilenameEnd - leftFilenameStart < right.Length)
                //    d_comparedString = right.Substring(rightFilenameStart, leftCoreFilenameEnd - leftFilenameStart);
                //else
                //    d_comparedString = right.Substring(rightFilenameStart);

                // "obvious" compare doesn't produce same results as CompareInfo.Compare()
                //int min = Math.Min(leftCoreFilenameEnd - leftFilenameStart, rightCoreFilenameEnd - rightFilenameStart);
                //for (int i = 0; i < min; i++) {
                //    char leftChar = char.ToLowerInvariant(left[leftFilenameStart + i]);
                //    char rightChar = char.ToLowerInvariant(right[rightFilenameStart + i]);
                //    if (leftChar != rightChar) {
                //        return (leftChar < rightChar) ? -1 : 1; 
                //    }
                //}
                //
                //if (leftCoreFilenameEnd - leftFilenameStart != rightCoreFilenameEnd - rightFilenameStart) {
                //    return (leftCoreFilenameEnd - leftFilenameStart < rightCoreFilenameEnd - rightFilenameStart)
                //        ? -1 : 1;
                //}

                int res = globCompare.Compare(left, leftFilenameStart, leftCoreFilenameEnd - leftFilenameStart,
                    right, rightFilenameStart, rightCoreFilenameEnd - rightFilenameStart);
                if (res != 0) return res;

                int leftFileNumber = GetFileNumber(left, leftCoreFilenameEnd, leftFileNumberLength);
                int rightFileNumber = GetFileNumber(right, rightCoreFilenameEnd, rightFileNumberLength);
                if (leftFileNumber != rightFileNumber) {
                    return (leftFileNumber < rightFileNumber) ? -1 : 1;
                }
                // finally, check file extension the lazy way
                res = string.Compare(left, right);
                return res;
            }

            private static void GetFilenameWithoutNumber(string fn, out string stem, out int number) {
                int i = 0;
                for (i = fn.Length - 1; i >= 0; i--) {
                    if (!char.IsDigit(fn[i])) break;
                }
                stem = fn.Substring(0, i + 1);
                string numberStr = fn.Substring(i + 1);
                if (numberStr.Length == 0)
                    number = -1;
                else
                    number = int.Parse(numberStr);
            }

            private static int CompareFilename(string left, string right) {
                string leftTrunc;
                int leftNum;
                GetFilenameWithoutNumber(Path.GetFileNameWithoutExtension(left), out leftTrunc, out leftNum);
                string rightTrunc;
                int rightNum;
                GetFilenameWithoutNumber(Path.GetFileNameWithoutExtension(right), out rightTrunc, out rightNum);

                int res = String.Compare(leftTrunc, rightTrunc);
                if (res != 0) return res;

                res = leftNum - rightNum;
                if (res != 0)
                    return (res < 0) ? -1 : 1;

                res = string.Compare(left, right);
                return res;
            }
        }

        public static string[] Persist(IEnumerable<ImageOrigin> origins) {
            string[] lines = new string[] { "Filename,Id,Volume,Volume Serial Number,Status,Type,Date,Caption,Notes,Rating,Hidden,Tags, Qualified Tags,Albums,Qualified Albums,Album Positions,Stack Top,Stack Position,Version Set Top,Version Set Position,db:guid,EXIF:Date,xmp:CreateDate,pse:FileNameOriginal,pse:FileDateOriginal,pse:FileSizeOriginal,tiff:ImageWidth,tiff:ImageHeight,pse:FileDate,pse:FileSize,pse:guid,exif:Make,exif:Model,exif:FNumber,exif:ExposureTime,exif:ExposureBias,exif:ExposureProgram,exif:FocalLength,exif:Flash,exif:ISOSpeedRatings,xmpDM:Duration,exif:GPSLongitude,exif:GPSLatitude,dc:creator,exif:RelatedSoundFile,pse:ProxyFile,dc:title,pse:LastOrganizerModifiedTime,pre:FrameRate,pre:MediaDuration,pre:MediaFormat,pre:Resolution,rotation,flip" }
                .Concat(origins.Select(o => string.Format("{0},,,,,,,,,,,,{1},,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,{2},{3}", o.SourcePath,
                    string.Join("^", o.Tags.Select(t => t.QualifiedName).ToArray()),
                    Math.Round(o.Rotation), o.Flip
                    )))
                    .ToArray();
            int first = lines[0].Split(new char[] { ',' }).Length;
            int second = lines[1].Split(new char[] { ',' }).Length;
            Debug.Assert(first == second);
            var result = lines;//.ToArray();
            return result;
        }

        public ImageOrigin(string sourcePath, string? targetPath) : this(sourcePath, targetPath, new ObservableCollection<PhotoTag>()) {
        }

        public ImageOrigin(string sourcePath, string? targetPath, ObservableCollection<PhotoTag> tags) {
            this.sourcePath = sourcePath;
            this.targetPath = targetPath;
            this.tags = tags;
        }

        public ObservableCollection<PhotoTag> Tags {
            get { return tags; }
        }

        protected override void NotifyPropertyChanged(string info) {
            base.NotifyPropertyChanged(info);
            RootControl.Instance.changesToSave = true;
        }

        // removes dups
        public void AddTag(PhotoTag tag) {
            if (this.HasTag(tag))
                return;

            // hmm, gratuitous -- can only be 0 or 1 in length...
            PhotoTag[] toRemove = this.Tags.Where(t => t.IsPrefixOf(tag)).ToArray(); // copy so we can modify Tags
            foreach (PhotoTag t in toRemove) {
                this.Tags.Remove(t);
            }
            this.Tags.Add(tag);
            RootControl.Instance.changesToSave = true;
        }

        // only removes exact matches -- bug or feature?
        public void RemoveTag(PhotoTag tag) {
            //if (this.HasTag(tag))
            //    return;

            this.Tags.Remove(tag);
            RootControl.Instance.changesToSave = true;
        }

        public bool HasTag(PhotoTag tag) {
            return PhotoTag.Matches(tag, this.Tags);
        }

        public string DisplayName {
            get { return Path.GetFileName(SourcePath); }
        }

        public Uri SourceUri {
            get { return new Uri(SourcePath, UriKind.RelativeOrAbsolute); }
        }

        public string SourcePath {
            get { return sourcePath; }
        }

        public string SourceDirectory {
            get { return Path.GetDirectoryName(SourcePath); }
        }

        public string TargetPath {
            get { return targetPath; }
        }

        // it's weird that we keep selection information in the ImageOrigin, but ImageInfos 
        // get thrown away and re-created while ImageOrigins do not
        public bool IsSelected {
            get { return isSelected; }
            set { isSelected = value; NotifyPropertyChanged("IsSelected"); }
        }

        public double Rotation {
            get { return rotation; }
            set {
                rotation = value;
                NotifyPropertyChanged("Rotation");
            }
        }

        public bool Flip {
            get { return flip; }
            set {
                flip = value;
                NotifyPropertyChanged("Flip");
            }
        }

        public override string ToString() {
            return "ImageOrigin " + DisplayName;
        }

        public static int GetIndex(ImageOrigin[] imageOrigins, ImageOrigin current) {
            // there's no IndexOf on arrays!
            int index = 0;
            if (current != null) {
                for (int i = 0; i < imageOrigins.Length; i++) {
                    if (imageOrigins[i] == current) {
                        index = i;
                        break;
                    }
                }
            }
            return index;
        }

        public static ImageOrigin NextImage(ImageOrigin[] imageOrigins, ImageOrigin current, int increment) {
            if (imageOrigins.Length == 0)
                return null;
            return NextImage(imageOrigins, GetIndex(imageOrigins, current), increment);
        }

        public static ImageOrigin NextImage(ImageOrigin[] imageOrigins, int index, int increment) {
            if (imageOrigins.Length == 0)
                return null;
            int nextIndex = NextIndex(imageOrigins, index, increment);
            return imageOrigins[nextIndex];
        }

        public static int NextIndex(ImageOrigin[] imageOrigins, int index, int increment) {
            index += increment;
            if (index >= imageOrigins.Length)
                index = 0;
            if (index < 0)
                index = imageOrigins.Length - 1;
            return index;
        }

        public static int NextIndexWrap(ImageOrigin[] imageOrigins, int index, int increment) {
            index += increment;
            if (index >= imageOrigins.Length)
                index = 0;
            if (index < 0)
                index += imageOrigins.Length;
            return index;
        }

    }

}