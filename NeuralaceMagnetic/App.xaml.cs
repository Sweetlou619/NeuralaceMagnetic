using NeuralaceMagnetic.Controls;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public SolidColorBrush ThemeColor = (SolidColorBrush)(new BrushConverter().ConvertFrom("#10D5A6"));

        public UniversalRobotController URController = new UniversalRobotController();
        public URSecondaryController URSecondController = new URSecondaryController();
        public UniversalRobotNoPendantController URNoPendantControl = new UniversalRobotNoPendantController();
        public PolarisCameraController PolarisController = new PolarisCameraController();
        public CameraURCoordinateTranslator CoordinateTranslator = new CameraURCoordinateTranslator();
        public ApplicationSettings ApplicationSettings = new ApplicationSettings();
        public ForceTorqueSensorController ForceTorqueSensor = new ForceTorqueSensorController();
        public TorqueSensorTracking TorqueSensorTracking = new TorqueSensorTracking();
        public bool MachineHomed = false;

        public void StartAllCommunications()
        {
            URController.Start();
            PolarisController.Start();
            URSecondController.Start();
            URNoPendantControl.Start();
            ForceTorqueSensor.Start();
            TorqueSensorTracking.Start();
        }

        public static new App Current
        {
            get
            {
                return (App)Application.Current;
            }
        }

        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(false, "Global\\" + appGuid))
            {
                if (!mutex.WaitOne(0, false))
                {
                    MessageBox.Show("The NeuraLace Axon Therapy Application is already running!");
                    return;
                }

                Process[] processList = Process.GetProcessesByName("Track");
                if (processList.Count() == 0)
                {
                    Process.Start(@"C:\Program Files\Northern Digital Inc\ToolBox\Track.exe");
                    Thread.Sleep(5000);
                }
                NeuralaceMagnetic.App app = new NeuralaceMagnetic.App();
                app.InitializeComponent();
                MainWindow mw = new MainWindow();
                app.Run(mw);
            }
        }

        private static string appGuid = "c0a76b5a-12ab-45c5-b9d9-d693faa6e7b9";
    }
}