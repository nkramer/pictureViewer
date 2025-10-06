using Pictureviewer.Core;
using Pictureviewer.Utilities;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pictureviewer.Slides {
    public partial class SlideShow : UserControl {
        private void CreateCommands() {
            Command command;

            command = new Command();
            command.Text = "Exit slideshow";
            //command.Button = closeButton;
            //command.HasMenuItem = false;
            command.Key = Key.Escape;
            command.Execute += delegate () {
                root.OnSlideshowExit(this);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Select current image"; // really toggle
            command.Key = Key.Enter;
            command.Execute += delegate () {
                typeaheadImage.IsSelected = !typeaheadImage.IsSelected;
                UpdateTextBlock();
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Select current image"; // really toggle
            command.Key = Key.LeftCtrl;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                typeaheadImage.IsSelected = !typeaheadImage.IsSelected;
                UpdateTextBlock();
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Select current image";
            command.DisplayKey = "Enter";
            command.Button = selectButton;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                typeaheadImage.IsSelected = true;
                UpdateTextBlock();
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Unselect current image";
            command.Button = unselectButton;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                typeaheadImage.IsSelected = false;
                UpdateTextBlock();
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Even better";
            command.Key = Key.E;
            command.ModifierKeys = ModifierKeys.Control;
            command.Execute += delegate () {
                var curIndex = ImageOrigin.GetIndex(displaySet, typeaheadImage);
                int i;
                for (i = curIndex - 1; i >= 0; i--) {
                    if (displaySet[i].IsSelected)
                        break;
                }

                if (i > -1) {
                    displaySet[i].IsSelected = false;
                    //root.fileListSource.UpdateTargetDirectory(displaySet[i]);
                }

                typeaheadImage.IsSelected = true;
                UpdateTextBlock();
            };
            commands.AddCommand(command);

            commands.AddMenuSeparator();

            command = new Command();
            command.Text = "Pause";
            command.Key = Key.Space;
            command.Button = pauseButton;
            command.Execute += delegate () {
                TogglePaused();
            };
            commands.AddCommand(command);
#if WPF
            commands.AddBinding(command, MediaCommands.Pause);
            commands.AddBinding(command, MediaCommands.TogglePlayPause); // I don't know which my remote control has
#endif

            command = new Command();
            command.Text = "Next picture";
            command.Key = Key.Right;
            command.Button = nextSlideButton;
            command.Execute += delegate () {
                UserMoveImage(1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Previous picture";
            command.Key = Key.Left;
            command.Button = previousSlideButton;
            command.Execute += delegate () {
                UserMoveImage(-1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Next picture";
            command.Key = Key.Down;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                UserMoveImage(1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Previous picture";
            command.Key = Key.Up;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                UserMoveImage(-1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Forward 10 pictures";
            command.Key = Key.PageDown;
            command.DisplayKey = "PageDown";
            command.Button = nextPageButton;
            command.Execute += delegate () {
                UserMoveImage(10);
            };
            commands.AddCommand(command);
#if WPF
            commands.AddBinding(command, MediaCommands.FastForward);
            commands.AddBinding(command, MediaCommands.NextTrack);
#endif

            command = new Command();
            command.Text = "Backward 10 pictures";
            command.Key = Key.PageUp;
            command.Button = previousPageButton;
            command.Execute += delegate () {
                UserMoveImage(-10);
            };
            commands.AddCommand(command);
#if WPF
            commands.AddBinding(command, MediaCommands.Rewind);
            commands.AddBinding(command, MediaCommands.PreviousTrack);
#endif

            commands.AddMenuSeparator();

            command = new Command();
            command.Text = "Grayscale";
            command.Key = Key.G;
            command.ModifierKeys = ModifierKeys.Control;
            command.Button = grayscaleButton;
            command.Execute += delegate () {
                imageDisplay.GrayscaleMode = !imageDisplay.GrayscaleMode;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Zoom";
            command.Key = Key.Z;
            command.ModifierKeys = ModifierKeys.Control;
            command.Button = zoomButton;
            command.Execute += delegate () {
                imageDisplay.Zoom = !imageDisplay.Zoom;
                if (imageDisplay.Zoom) {
                    pausedBeforeZoom = paused;
                    Paused = true;
                } else {
                    Paused = pausedBeforeZoom;
                }
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Rotate counterclockwise";
            command.Key = Key.R;
            command.ModifierKeys = ModifierKeys.Control;
            command.Button = rotateCounterclockwiseButton;
            command.Execute += delegate () {
                imageDisplay.ImageInfo.Origin.Rotation -= 90;
                imageDisplay.Rotation = imageDisplay.ImageInfo.Origin.Rotation;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Rotate clockwise";
            command.Key = Key.R;
            command.ModifierKeys = ModifierKeys.Control | ModifierKeys.Shift;
            command.Button = rotateClockwiseButton;
            command.Execute += delegate () {
                imageDisplay.ImageInfo.Origin.Rotation += 90;
                imageDisplay.Rotation = imageDisplay.ImageInfo.Origin.Rotation;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Flip";
            command.Key = Key.F;
            command.ModifierKeys = ModifierKeys.Control;
            command.Execute += delegate () {
                var origin = imageDisplay.ImageInfo.Origin;
                origin.Flip = !origin.Flip;
                imageDisplay.Flip = origin.Flip;
            };
            commands.AddCommand(command);


            commands.AddMenuSeparator();

            //command = new Command();
            //command.Text = "Open folder";
            //command.Key = Key.O;
            //command.ModifierKeys = ModifierKeys.Control;
            //command.Button = openFolderButton;
            //command.Execute += delegate() {
            //    SelectDirectories(false/* first time*/);
            //};
            //commands.AddCommand(command);

            //command = new Command();
            //command.Text = "Photo grid";
            //command.Key = Key.P;
            //command.ModifierKeys = ModifierKeys.Control;
            //command.Button = openFolderButton;
            //command.Execute += delegate() {
            //    ShowPhotoGrid();
            //};
            //commands.AddCommand(command);

            command = new Command();
            command.Text = "Increase shot clock speed";
#if WPF
            command.Key = Key.OemPlus;
#endif
            command.DisplayKey = "+";
            command.ModifierKeys = ModifierKeys.Control;
            command.Button = increaseSpeedButton;
            command.Execute += delegate () {
                shotclockSpeed--;
                if (shotclockSpeed < 1)
                    shotclockSpeed = 1;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Decrease shot clock speed";
#if WPF
            command.Key = Key.OemMinus;
#endif
            command.DisplayKey = "-";
            command.ModifierKeys = ModifierKeys.Control;
            command.Button = decreaseSpeedButton;
            command.Execute += delegate () {
                shotclockSpeed++;
            };
            commands.AddCommand(command);

            //command = new Command();
            //command.Text = "Show/hide debug info";
            //command.Key = Key.F2;
            //command.Execute += delegate() {
            //    ToggleMetadataDisplay();
            //};
            //commands.AddCommand(command);

            //command = new Command();
            //command.Text = "Toggle pixel-perfect mode";
            //command.Execute += delegate() {
            //    pixelPerfect = !pixelPerfect;
            //    ResetLoader();
            //};
            //commands.AddCommand(command);

            commands.AddMenuSeparator();

            command = new Command();
            command.Text = "No crop mark";
            command.Key = Key.D0;
            command.Execute += delegate () {
                this.imageDisplay.CropMarkAspectRatio = 0;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "square crop mark";
            command.Key = Key.D2;
            command.Execute += delegate () {
                this.imageDisplay.CropMarkAspectRatio = 1.0;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "3x5 crop mark";
            command.Key = Key.D3;
            command.Execute += delegate () {
                this.imageDisplay.CropMarkAspectRatio = 3.0 / 5.0;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "4x6 crop mark";
            command.Key = Key.D4;
            command.Execute += delegate () {
                this.imageDisplay.CropMarkAspectRatio = 4.0 / 6.0;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "5x7 crop mark";
            command.Key = Key.D5;
            command.Execute += delegate () {
                this.imageDisplay.CropMarkAspectRatio = 5.0 / 7.0;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "8x10 crop mark";
            command.Key = Key.D8;
            command.Execute += delegate () {
                this.imageDisplay.CropMarkAspectRatio = 8.0 / 10.0;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "11x14 crop mark";
            command.Key = Key.D1;
            command.Execute += delegate () {
                this.imageDisplay.CropMarkAspectRatio = 11.0 / 14.0;
            };
            commands.AddCommand(command);

#if SILVERLIGHT
            grayscaleButton.Visibility= Visibility.Collapsed;
            openFolderButton.Visibility= Visibility.Collapsed;
            minimizeButton.Visibility = Visibility.Collapsed;
            toolbarRow2.Children.Remove(increaseSpeedButton);
            toolbarRow1.Children.Insert(0, increaseSpeedButton);
#endif
        }

    }
}