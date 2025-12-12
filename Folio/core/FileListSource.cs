using System;
using System.Windows;

namespace Folio.Core {
    public class SelectDirectoriesCompletedEventArgs : EventArgs {
        public ImageOrigin[]? imageOrigins;
        public ImageOrigin? initialFocus;
    }

    // where the program gets files from, differs for web versus desktop apps.
    // Note that you'll want to choose a FileListSource that's compatible 
    // with your ImageInfo implementation.
    public abstract class FileListSource {
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
