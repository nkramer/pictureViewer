using Folio.Core;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace Folio.Library {
    public partial class SelectableImageDisplay : UserControl, INotifyPropertyChanged {
        public SelectableImageDisplay() {
            InitializeComponent();
            DataContext = this;
        }

        public ImageDisplay ImageDisplay { get { return display; } }

        // HACK: seems easier to implement INotifyPropertyChanged than make everything a dependency property
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(String info) {
            if (PropertyChanged != null) {
                var args = new PropertyChangedEventArgs(info);
                PropertyChanged(this, args);
            }
        }

        private bool isFocusedImage = false;

        public bool IsFocusedImage {
            get { return isFocusedImage; }
            set { isFocusedImage = value; NotifyPropertyChanged("IsFocusedImage"); }
        }
    }
}
