using System.Diagnostics;
using Pictureviewer.Utilities;

namespace Pictureviewer.Book
{
    public class TwoPages : ChangeableObject
    {
        private PhotoPageModel left;
        private PhotoPageModel right;

        public TwoPages(PhotoPageModel l, PhotoPageModel r)
        {
            this.left = l;
            this.right = r;
            Debug.Assert(left != null || right != null);
        }

        public PhotoPageModel Left
        {
            get { return left; }
            set { left = value; NotifyPropertyChanged("Left"); }
        }

        public PhotoPageModel Right
        {
            get { return right; }
            set { right = value; NotifyPropertyChanged("Right"); }
        }
    }
}
