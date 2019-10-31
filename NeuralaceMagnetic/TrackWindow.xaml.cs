using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for TrackWindow.xaml
    /// </summary>
    public partial class TrackWindow : Window, INotifyPropertyChanged
    {
        private double centerPixelOffset = 155;
        private DispatcherTimer uiTimer;
        private Thread backgroundThread;
        private Controls.TrackCameraWithRobot robotTrack;
        private Controls.UniversalRobotController.URRobotCoOrdinate robotStartLocation;

        private double _AccelerationSpeed = 200;
        public double AccelerationSpeed
        {
            get
            {
                return _AccelerationSpeed;
            }
            set
            {
                _AccelerationSpeed = value;
                robotTrack.AccelerationSpeed = _AccelerationSpeed;
                OnPropertyChanged();
            }
        }

        private String _RobotStartLocationString;
        public String RobotStartLocationString
        {
            get
            {
                return _RobotStartLocationString;
            }

            set
            {
                _RobotStartLocationString = value;
                OnPropertyChanged();
            }
        }

        private String _DeltaRobotLocationString;
        public String DeltaRobotLocationString
        {
            get
            {
                return _DeltaRobotLocationString;
            }

            set
            {
                _DeltaRobotLocationString = value;
                OnPropertyChanged();
            }
        }

        public TrackWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            robotTrack = new Controls.TrackCameraWithRobot(
            App.Current.URController,
            App.Current.URSecondController,
            App.Current.PolarisController,
            App.Current.ApplicationSettings,
            App.Current.CoordinateTranslator,
            App.Current.TorqueSensorTracking,
            _AccelerationSpeed
            );

            robotStartLocation = App.Current.URController.GetCurrentLocation();
            RobotStartLocationString = robotStartLocation.ToString();
            
            //TODO: Get position of marker
        }

        void CreateUIUpdateThread()
        {
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(100);
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                Controls.UniversalRobotController.URRobotCoOrdinate current = App.Current.URController.GetCurrentLocation();
                double xOffset = robotTrack.CurrentSetPoint.x - current.x;
                double yOffset = robotTrack.CurrentSetPoint.y - current.y;
                xOffset = Math.Round(xOffset, 4);
                yOffset = Math.Round(yOffset, 4);
                xOffset *= 1000;
                yOffset *= 1000;

                TargetFollower.Margin = new Thickness(
                    centerPixelOffset + xOffset,
                    centerPixelOffset + yOffset,
                    0, 0);

                Controls.UniversalRobotController.URRobotCoOrdinate deltaRobot = robotStartLocation - current;
                DeltaRobotLocationString = deltaRobot.ToString();
            }));
        }

        void DoTracking()
        {
            robotTrack.ErrorOccured += RobotTrack_ErrorOccured;
            robotTrack.InitOccured += robotTrack_InitOccured;
            robotTrack.Start();
        }

        void robotTrack_InitOccured(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        if (App.Current.ApplicationSettings.TrackTOFSensor)
                        {
                            statusLabel.Content = "Current ToF tracking distance: " + robotTrack.GetLaserSetPoint() + " mm";
                        }
                        else
                        {
                            statusLabel.Content = "ToF tracking is disabled.";
                        }
                    }));
        }

        private void RobotTrack_ErrorOccured(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                statusLabel.Content = robotTrack.LastErrorMessage;
                statusLabel.Foreground = Brushes.Tomato;
                MessageBox.Show(robotTrack.LastErrorMessage, "An Error has occured!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                this.DoneButton.Focus();
            }));
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            robotTrack.Stop();

            this.DialogResult = true;
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            robotTrack.Stop();

            if (uiTimer != null)
            {
                uiTimer.Stop();
            }

            if (backgroundThread != null)
            {
                backgroundThread.Abort();
            }
            
            App.Current.TorqueSensorTracking.SetForceTracking(false);
            App.Current.URController.DisableTrackingMotionSettings();
            App.Current.URController.SetVirtualEStopOverride(false);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            backgroundThread = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    for (int i = 3; i > 0; i--)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                        {
                            statusLabel.Content = "Tracking starting in: " + i.ToString();
                        }));
                        SystemSounds.Beep.Play();
                        Thread.Sleep(1000);
                    }

                    SystemSounds.Exclamation.Play();

                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        App.Current.URController.UseTrackingMotionSettings();
                        CreateUIUpdateThread();
                        DoTracking();
                    }));
                });
            
            backgroundThread.Start();
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}
