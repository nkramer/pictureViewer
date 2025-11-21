using System;
using System.Windows;
using System.Windows.Media;

namespace Folio.Utilities
{
    public partial class ThemedMessageBox : BaseDialog
    {
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(ThemedMessageBox), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(MessageBoxImage), typeof(ThemedMessageBox), new PropertyMetadata(MessageBoxImage.None, OnIconChanged));

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public MessageBoxImage Icon
        {
            get { return (MessageBoxImage)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        private MessageBoxResult _result = MessageBoxResult.None;

        private ThemedMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();

            Message = message;
            DialogTitle = title;
            Icon = icon;
            Buttons = MapButtons(button);

            UpdateIconVisual(icon);
        }

        protected override void OnOk()
        {
            _result = MessageBoxResult.OK;
            base.OnOk();
        }

        protected override void OnCancel()
        {
            // Determine the result based on the button configuration
            if (Buttons == DialogButtons.OkCancel)
            {
                _result = MessageBoxResult.Cancel;
            }
            else if (Buttons == DialogButtons.Cancel)
            {
                _result = MessageBoxResult.Cancel;
            }
            else
            {
                _result = MessageBoxResult.No;
            }
            base.OnCancel();
        }

        private static DialogButtons MapButtons(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    return DialogButtons.Ok;
                case MessageBoxButton.OKCancel:
                    return DialogButtons.OkCancel;
                case MessageBoxButton.YesNo:
                    return DialogButtons.OkCancel; // Map Yes/No to Ok/Cancel visually
                case MessageBoxButton.YesNoCancel:
                    return DialogButtons.OkCancel; // Simplified mapping
                default:
                    return DialogButtons.Ok;
            }
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ThemedMessageBox dialog)
            {
                dialog.UpdateIconVisual((MessageBoxImage)e.NewValue);
            }
        }

        private void UpdateIconVisual(MessageBoxImage icon)
        {
            if (IconPath == null)
                return;

            switch (icon)
            {
                case MessageBoxImage.Information:
                    // Information icon (i in a circle)
                    IconPath.Data = Geometry.Parse("M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10s10-4.48,10-10S17.52,2,12,2z M13,17h-2v-6h2V17z M13,9h-2V7h2V9z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(0, 120, 215)); // Blue
                    IconPath.Visibility = Visibility.Visible;
                    break;

                case MessageBoxImage.Warning: // aka MessageBoxImage.Exclamation:
                    // Warning icon (! in a triangle)
                    IconPath.Data = Geometry.Parse("M1,21h22L12,2L1,21z M13,18h-2v-2h2V18z M13,14h-2v-4h2V14z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(255, 185, 0)); // Orange/Yellow
                    IconPath.Visibility = Visibility.Visible;
                    break;

                case MessageBoxImage.Error: // aka MessageBoxImage.Stop, MessageBoxImage.Hand:
                    // Error icon (X in a circle)
                    IconPath.Data = Geometry.Parse("M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10s10-4.48,10-10S17.52,2,12,2z M17,15.59L15.59,17L12,13.41L8.41,17L7,15.59L10.59,12L7,8.41L8.41,7L12,10.59L15.59,7L17,8.41L13.41,12L17,15.59z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(232, 17, 35)); // Red
                    IconPath.Visibility = Visibility.Visible;
                    break;

                case MessageBoxImage.Question:
                    // Question icon (? in a circle)
                    IconPath.Data = Geometry.Parse("M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10s10-4.48,10-10S17.52,2,12,2z M13,19h-2v-2h2V19z M15.07,11.25l-0.9,0.92C13.45,12.9,13,13.5,13,15h-2v-0.5c0-1.1,0.45-2.1,1.17-2.83l1.24-1.26C13.78,10.04,14,9.55,14,9c0-1.1-0.9-2-2-2s-2,0.9-2,2H8c0-2.21,1.79-4,4-4s4,1.79,4,4C16,9.88,15.64,10.68,15.07,11.25z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(0, 120, 215)); // Blue
                    IconPath.Visibility = Visibility.Visible;
                    break;

                case MessageBoxImage.None:
                default:
                    IconPath.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        // Static Show methods matching MessageBox API

        public static MessageBoxResult Show(string messageBoxText)
        {
            return Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return Show(messageBoxText, caption, button, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            // If no caption provided, use message as title if short, otherwise use "Warning"
            if (string.IsNullOrEmpty(caption))
            {
                caption = messageBoxText.Length < 20 ? messageBoxText : "Warning";
            }

            var dialog = new ThemedMessageBox(messageBoxText, caption, button, icon);
            bool? result = dialog.ShowDialog();

            // Map the result based on button type
            if (button == MessageBoxButton.YesNo || button == MessageBoxButton.YesNoCancel)
            {
                // For Yes/No dialogs, map OK to Yes and Cancel/No to No
                if (result == true)
                    return MessageBoxResult.Yes;
                else
                    return MessageBoxResult.No;
            }
            else if (button == MessageBoxButton.OKCancel)
            {
                if (result == true)
                    return MessageBoxResult.OK;
                else
                    return MessageBoxResult.Cancel;
            }
            else // MessageBoxButton.OK
            {
                return MessageBoxResult.OK;
            }
        }
    }
}
