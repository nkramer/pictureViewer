using System;
using System.IO;
using System.Windows;
using Serilog;

namespace Folio {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : Application {

        public App() {
            InitializeComponent();
            this.Startup += new StartupEventHandler(App_Startup);
            this.Exit += new ExitEventHandler(App_Exit);

            // Configure Serilog
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Folio",
                "logs",
                "folio-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                //.WriteTo.File(
                //    logPath,
                //    rollingInterval: RollingInterval.Day,
                //    retainedFileCountLimit: 7,
                //    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}")
                //.WriteTo.Seq("http://localhost:5341")
                .CreateLogger();

            Log.Information("Folio application starting");
        }

        void App_Exit(object sender, ExitEventArgs e) {
            Log.Information("Folio application shutting down");
            Log.CloseAndFlush();
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