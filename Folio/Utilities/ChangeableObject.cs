using System;
using System.ComponentModel;

namespace Folio.Utilities;
public class ChangeableObject : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void NotifyPropertyChanged(String info) {
        if (PropertyChanged != null) {
            var args = new PropertyChangedEventArgs(info);
            PropertyChanged(this, args);
        }
    }
}
