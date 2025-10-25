using System.Windows;

namespace Folio {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : Application {

        public App() {
            InitializeComponent();
            this.Startup += new StartupEventHandler(App_Startup);
        }

        void App_Startup(object sender, StartupEventArgs e) {
            foreach (string s in e.Args) {
                if (s.StartsWith("-source="))
                    InitialSourceDirectory = s.Substring("-source=".Length);
                else if (s.StartsWith("-target="))
                    InitialTargetDirectory = s.Substring("-target=".Length);
                else if (s == "-enableEscapeKey")
                    EnableEscapeKey = true;
                else {
                    MessageBox.Show("Unknown commandline option.  Usage:\n" +
                        "Folio.exe [-source=<directory>] [-target=<directory>] [-enableEscapeKey]");
                    this.Shutdown(1);
                }
            }
        }

        // my debugging command line:
        // -source=c:\pics "-target=c:\good 2008-07" -enableEscapeKey
        public static string InitialSourceDirectory = null;
        public static string InitialTargetDirectory = null;
        public static bool EnableEscapeKey = false;

    }
}