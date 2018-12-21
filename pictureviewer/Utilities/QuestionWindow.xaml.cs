using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace pictureviewer
{
	/// <summary>
	/// Interaction logic for QuestionWindow.xaml
	/// </summary>
	public partial class QuestionWindow : Window
	{
		public QuestionWindow()
		{
			this.InitializeComponent();
            box.Focus();
            this.KeyDown += new KeyEventHandler(QuestionWindow_KeyDown);
			// Insert code required on object creation below this point.
		}

        void QuestionWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) {
                cancel_Click(null, null);
            }
        }

        public string Result
        {
            get { return box.Text; }
            set { box.Text = value; box.SelectAll(); }
        }

        public string Label
        {
            get { return textBlock.Text; }
            set { textBlock.Text = value; }
        }

        private void ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
	}
}