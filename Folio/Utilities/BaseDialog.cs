using Folio.Shell;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Folio.Utilities;
public enum DialogButtons {
    None,
    Ok,
    Cancel,
    OkCancel
}

// Base class for dialogs that provides common functionality:
// - Custom/borderless window
// - A title
// - proper margins & colors
// - Ok/Cancel buttons and Escape key handling
// - Draggable by title bar
public class BaseDialog : Window {
    public static readonly DependencyProperty DialogTitleProperty =
        DependencyProperty.Register("DialogTitle", typeof(string), typeof(BaseDialog),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ButtonsProperty =
        DependencyProperty.Register("Buttons", typeof(DialogButtons), typeof(BaseDialog),
            new PropertyMetadata(DialogButtons.OkCancel));

    private bool _isDragging;
    private Point _dragStartPoint;

    public DialogButtons Buttons {
        get { return (DialogButtons)GetValue(ButtonsProperty); }
        set { SetValue(ButtonsProperty, value); }
    }

    public string DialogTitle {
        get { return (string)GetValue(DialogTitleProperty); }
        set { SetValue(DialogTitleProperty, value); }
    }

    public BaseDialog() {
        this.WindowStyle = WindowStyle.None;
        this.ResizeMode = ResizeMode.NoResize;
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.SizeToContent = SizeToContent.WidthAndHeight;
        this.Background = (Brush)Application.Current.FindResource("midGray");
        this.KeyDown += BaseDialog_KeyDown;

        // Apply the template from WpfControlTemplates.xaml
        this.Template = (ControlTemplate)Application.Current.FindResource("BaseDialogTemplate");
    }

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();

        // Wire up button click handlers
        if (this.Template.FindName("OkButton", this) is Button okButton) {
            okButton.Click += (s, e) => OnOk();
        }
        if (this.Template.FindName("CancelButton", this) is Button cancelButton) {
            cancelButton.Click += (s, e) => OnCancel();
        }

        // Wire up drag handlers for the title bar
        if (this.Template.FindName("TitleBar", this) is FrameworkElement titleBar) {
            titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseLeftButtonUp += TitleBar_MouseLeftButtonUp;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (sender is FrameworkElement titleBar) {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            titleBar.CaptureMouse();
            e.Handled = true;
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e) {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed) {
            Point currentPosition = e.GetPosition(this);
            Point screenPosition = PointToScreen(currentPosition);
            Point startScreenPosition = PointToScreen(_dragStartPoint);

            this.Left += screenPosition.X - startScreenPosition.X;
            this.Top += screenPosition.Y - startScreenPosition.Y;
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (_isDragging && sender is FrameworkElement titleBar) {
            _isDragging = false;
            titleBar.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void BaseDialog_KeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            OnCancel();
        }
    }

    protected virtual void OnOk() {
        this.DialogResult = true;
        this.Close();
    }

    protected virtual void OnCancel() {
        // We can't set DialogResult unless we called ShowDialog()
        if (!RootControl.IsShowingAllDialogs)
            this.DialogResult = false;
        this.Close();
    }
}

// Converter that checks if a specific button should be visible based on DialogButtons value
// Parameter: "Ok" or "Cancel" to indicate which button
public class DialogButtonVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (parameter.Equals("Ok") && (value.Equals(DialogButtons.Ok) || value.Equals(DialogButtons.OkCancel)))
            return Visibility.Visible;
        if (parameter.Equals("Cancel") && (value.Equals(DialogButtons.Cancel) || value.Equals(DialogButtons.OkCancel)))
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
