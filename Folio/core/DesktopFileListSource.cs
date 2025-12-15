using Folio.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Folio.Core;
class DesktopFileListSource : FileListSource {
    public override void SelectOneDirectory(Action<SelectDirectoriesCompletedEventArgs> completedCallback) {
        var dialog = new SelectFolder2(this);
        dialog.SourceDirectory = sourceDirectory;
        dialog.ShowDialog();
        if (dialog.Canceled)
            return;

        var filenames = GetFiles(dialog.SourceDirectory!);

        var imageOrigins = new ImageOrigin[filenames.Length];
        int initialIndex = 0;
        for (int i = 0; i < filenames.Length; i++) {
            var origin = new ImageOrigin(filenames[i], null);
            imageOrigins[i] = origin;

            // UNDONE -- is this calculation going to be fast enough to do on thread?
            if (File.Exists(origin.TargetPath))
                initialIndex = i;
        }

        var args = new SelectDirectoriesCompletedEventArgs();
        args.imageOrigins = imageOrigins;
        args.initialFocus = imageOrigins[initialIndex];
        completedCallback(args);
    }

    public override void SelectDirectoriesForTriage(bool firstTime, Action<SelectDirectoriesCompletedEventArgs> completedCallback) {
        if (firstTime) {
            sourceDirectory = App.InitialSourceDirectory;
            targetDirectory = App.InitialTargetDirectory;
        }

        if (!firstTime || sourceDirectory == null) {
            var dialog = new SelectFolders(this);
            dialog.Owner = rootWindow;
            dialog.SourceDirectory = sourceDirectory;
            dialog.ShowDialog();
            if (dialog.Canceled)
                return;

            sourceDirectory = dialog.SourceDirectory!;
            targetDirectory = dialog.TargetDirectory!;
        }

        var filenames = GetFiles(sourceDirectory!);
        if (filenames.Length == 0)
            return;

        this.IsTriageMode = true;
        var imageOrigins = new ImageOrigin[filenames.Length];
        int initialIndex = 0;
        for (int i = 0; i < filenames.Length; i++) {
            var origin = new ImageOrigin(filenames[i],
                Path.Combine(targetDirectory!, Path.GetFileName(filenames[i])));
            imageOrigins[i] = origin;

            // UNDONE -- is this calculation going to be fast enough to do on thread?
            if (File.Exists(origin.TargetPath)) {
                initialIndex = i;
                origin.IsSelected = true; // do before prop change handler is hooked up
            }

            if (IsTriageMode) {
                origin.PropertyChanged += (s, e) => {
                    // origin.IsSelected changes
                    UpdateTargetDirectory(origin);
                };
            }
        }

        var args = new SelectDirectoriesCompletedEventArgs();
        args.imageOrigins = imageOrigins;
        args.initialFocus = imageOrigins[initialIndex];
        completedCallback(args);
    }

    private string[] GetFiles(string sourceDirectory) {
        var jpgs = Directory.GetFiles(sourceDirectory, "*.jpg");
        var pngs = Directory.GetFiles(sourceDirectory, "*.png");
        var bmps = Directory.GetFiles(sourceDirectory, "*.bmp");
        var heic = Directory.GetFiles(sourceDirectory, "*.heic");
        var filenames = jpgs.Concat(pngs).Concat(bmps).Concat(heic).ToArray();
        Array.Sort(filenames, new Comparison<string>(CompareFilename));
        return filenames;
    }

    private static void GetFilenameWithoutNumber(string fn, out string stem, out int number) {
        int i = 0;
        //for(i=0;i<fn.Length; i++) {
        for (i = fn.Length - 1; i >= 0; i--) {
            if (!char.IsDigit(fn[i])) break;
            if (fn.Length - i > 6) break; // don't consider more than 6 digits. Hack to avoid int overflow.
        }
        stem = fn.Substring(0, i + 1);
        string numberStr = fn.Substring(i + 1);
        if (numberStr.Length == 0)
            number = -1;
        else
            number = int.Parse(numberStr);
    }

    public static int CompareFilename(string left, string right) {
        string leftTrunc;
        int leftNum;
        GetFilenameWithoutNumber(Path.GetFileNameWithoutExtension(left), out leftTrunc, out leftNum);
        string rightTrunc;
        int rightNum;
        GetFilenameWithoutNumber(Path.GetFileNameWithoutExtension(right), out rightTrunc, out rightNum);

        int res = String.Compare(leftTrunc, rightTrunc);
        if (res != 0) return res;

        //int 
        //res = string.Compare(leftNum, rightNum);
        res = leftNum - rightNum;
        if (res != 0) return res;

        res = string.Compare(left, right);
        return res;
    }



    private void EnsureTargetDirectoryExists() {
        if (!Directory.Exists(targetDirectory)) {
            Directory.CreateDirectory(targetDirectory!);
        }
    }

    private void UpdateTargetDirectory(ImageOrigin image) {
        string sourceFile = image.SourcePath;
        string? targetFile = image.TargetPath;

        if (IsTriageMode) {

            if (image.IsSelected) {
                if (!File.Exists(targetFile)) {
                    EnsureTargetDirectoryExists();
                    // todo: rotate output file correctly
                    //                if (image.Rotation != 0) {
                    //                    //File.Copy(sourceFile, targetFile);

                    //                    //Stream pngStream = new System.IO.FileStream(targetFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    //                    //var pngDecoder = new JpegBitmapDecoder(pngStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                    //                    //BitmapFrame pngFrame = pngDecoder.Frames[0];
                    //                    //InPlaceBitmapMetadataWriter pngInplace = pngFrame.CreateInPlaceBitmapMetadataWriter();
                    //                    //if (pngInplace.TrySave() == true) { pngInplace.SetQuery("/app1/ifd/{ushort=274}", (short)2); }
                    //                    //pngStream.Close();

                    //                    BitmapDecoder original = BitmapDecoder.Create(image.SourceUri,

                    //BitmapCreateOptions.PreservePixelFormat,// | BitmapCreateOptions.IgnoreColorProfile,
                    //BitmapCacheOption.None);
                    //                    var md = (BitmapMetadata) original.Frames[0].Metadata.Clone();

                    //                    var transform = new RotateTransform();
                    //                    transform.Angle = image.Rotation;
                    //                    var t = new TransformedBitmap(original.Frames[0], transform);
                    //                    var encoder = new PngBitmapEncoder();
                    //                                        //output.Frames.Add(BitmapFrame.Create(original.Frames[0], original.Frames[0].Thumbnail, metadata, original.Frames[0].ColorContexts));

                    //                    var frame = BitmapFrame.Create(t, 
                    //                        null, //original.Frames[0].Thumbnail, 
                    //                        md, null);
                    //                        //original.Frames[0].ColorContexts);
                    //                    //((BitmapFrameEncode)frame);
                    //                    //pn

                    //                    encoder.Frames.Add(frame);
                    //                    //encoder.Metadata = (BitmapMetadata) md;
                    //                    //encoder.Frames[0].Metadata = md;

                    //                    var stream = new MemoryStream();
                    //                    encoder.Save(stream);
                    //                    var bytes = stream.ToArray();
                    //                    File.WriteAllBytes(targetFile, bytes);
                    //                } else {
                    File.Copy(sourceFile, targetFile!);
                    //                    }
                } else if (new FileInfo(sourceFile).Length != new FileInfo(targetFile!).Length) {
                    ThemedMessageBox.Show("can't copy, file already exists");
                } else {
                    // assume the file that's already there is identical
                }
            } else { // image not selected
                if (File.Exists(targetFile)) {
                    if (new FileInfo(sourceFile).Length != new FileInfo(targetFile).Length) {
                        ThemedMessageBox.Show("can't delete, file already exists but is different");
                    } else {
                        File.Delete(targetFile);
                    }
                }
            }

            // debug code/assert
            if (image.IsSelected != File.Exists(targetFile)) {
                Debug.Assert(new FileInfo(sourceFile).Length != new FileInfo(targetFile!).Length);
            }
        }
    }

    public override void ShowHelp() {
        var executable = Process.GetCurrentProcess().MainModule!.FileName;
        var directory = Path.GetDirectoryName(executable);
        var helpfile = Path.Combine(directory!, @"Help.html");
        Process.Start("iExplore.exe", helpfile);
    }
}
