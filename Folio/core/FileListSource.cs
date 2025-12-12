using System;
using System.Windows;

namespace Folio.Core {
    public class SelectDirectoriesCompletedEventArgs : EventArgs {
        public ImageOrigin[]? imageOrigins; // Todo - make a proper constructor for this class and get rid of the ?
        public ImageOrigin? initialFocus;
    }

    // where the program gets files from, differs for web versus desktop apps.
    // Note that you'll want to choose a FileListSource that's compatible 
    // with your ImageInfo implementation.
    public abstract class FileListSource {
        // Todo: fix this convoluted interface. Consider removing the abstraction entirely since there's only one subclass. 
        public string? sourceDirectory; // read-only except to subclasses
        public string? targetDirectory; // read-only except to subclasses

        public Window? rootWindow;

        public bool IsTriageMode; // whether to copy selected files from src to target dir

        // may display UI.  If the dialog is canceled, the callback won't be called.
        public abstract void SelectDirectoriesForTriage(bool firstTime, Action<SelectDirectoriesCompletedEventArgs> completedCallback);

        // may display UI.  If the dialog is canceled, the callback won't be called.
        public abstract void SelectOneDirectory(Action<SelectDirectoriesCompletedEventArgs> completedCallback);

        // copy/delete the file from the target directory
        //public abstract void UpdateTargetDirectory(ImageOrigin image);

        public virtual bool IsSourceDirectory(string directory) {
            return (directory == sourceDirectory);
        }

        public virtual void ShowHelp() {
        }
    }
}
