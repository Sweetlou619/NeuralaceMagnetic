using NeuralaceMagnetic.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer uiTimer;


        public MainWindow()
        {
            InitializeComponent();

            CreateUIUpdateThread();
            App.Current.StartAllCommunications();
        }

        void CreateUIUpdateThread()
        {
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(100);
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        double radtoang(double rad)
        {
            double ang = rad * (180 / Math.PI);
            if (ang > 360)
            {
                while (ang > 360)
                {
                    ang -= 360;
                }
            }
            if (ang < -360)
            {
                while (ang < -360)
                {
                    ang += 360;
                }
            }
            return ang;
        }

        void UpdateStatusLeds()
        {
            //UR
            URConnected.Fill = App.Current.URController.IsUniversalRobotConnected ? App.Current.ThemeColor : Brushes.Gray;
            URReady.Fill = App.Current.URController.URRobotStatus.RobotMode == 7 ? App.Current.ThemeColor : Brushes.Gray;
            URDistanceInRange.Fill = !double.IsNaN(UniversalRobotHelperFunctions.GetCurrentZDistance()) ? App.Current.ThemeColor : Brushes.Gray;
            URHomed.Fill = App.Current.MachineHomed ? App.Current.ThemeColor : Brushes.Gray;
            URFault.Fill = App.Current.URSecondController.GetCurrentSafetyStatus() != URSecondaryController.SafetyType.Normal ? Brushes.Tomato : Brushes.Gray;
            URFreedrive.Fill = App.Current.URController.GetFreeDriveStatus() ? App.Current.ThemeColor : Brushes.Gray;
            URErrorMessageBox.Text = App.Current.URSecondController.GetCurrentSafetyStatus().ToString();
            SettingsOn.Content = App.Current.URController.URRobotStatus.RobotMode == 7 ? "Off" : "On";
            double currentDistanceMM = UniversalRobotHelperFunctions.GetCurrentZDistance();
            if (double.IsNaN(currentDistanceMM))
            {
                URDistanceValue.Text = "Out of range.";
            }
            else
            {
                double roundedDistance = Math.Round(currentDistanceMM);
                URDistanceValue.Text = roundedDistance.ToString() + " mm";
            }
            //polaris
            PolarisConnected.Fill = App.Current.PolarisController.IsPolarisConnected ? App.Current.ThemeColor : Brushes.Gray;
            RobotMarkerVisible.Fill = App.Current.PolarisController.GetURRobotRigidBody().isInRange ? App.Current.ThemeColor : Brushes.Gray;
            Patientent1MarkerVisible.Fill = App.Current.PolarisController.GetUserRigidBodyOne().isInRange ? App.Current.ThemeColor : Brushes.Gray;
            PatientMarker2Visible.Fill = App.Current.PolarisController.GetUserRigidBodyTwo().isInRange ? App.Current.ThemeColor : Brushes.Gray;

            //force sensor
            ForceOverLimit.Fill = UniversalRobotHelperFunctions.IsForceOverLimit() ? Brushes.Tomato : Brushes.Gray;
            TrackButton.IsEnabled = true; //App.Current.MachineHomed;
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                UpdateStatusLeds();

                TextBoxX.Text = App.Current.URController.URRobotStatus.ToolVectorActual_1.ToString();
                TextBoxY.Text = App.Current.URController.URRobotStatus.ToolVectorActual_2.ToString();
                TextBoxZ.Text = App.Current.URController.URRobotStatus.ToolVectorActual_3.ToString();
                //TextBoxQX.Text = App.Current.URController.URRobotStatus.ToolVectorActual_4.ToString();
                //TextBoxQY.Text = App.Current.URController.URRobotStatus.ToolVectorActual_5.ToString();
                //TextBoxQZ.Text = App.Current.URController.URRobotStatus.ToolVectorActual_6.ToString();
                Vector3D angle = new Vector3D(App.Current.URController.URRobotStatus.ToolVectorActual_4,
                    App.Current.URController.URRobotStatus.ToolVectorActual_5,
                    App.Current.URController.URRobotStatus.ToolVectorActual_6);
                Vector3D rpy = App.Current.CoordinateTranslator.ToRollPitchYawV2(angle);
                TextBoxQX.Text = radtoang(rpy.X).ToString();
                TextBoxQY.Text = radtoang(rpy.Y).ToString();
                TextBoxQZ.Text = radtoang(rpy.Z).ToString();


                LabelStatus.Content = App.Current.URController.IsUniversalRobotConnected ? "Connected" : "Connecting...";
                LabelStatus.Foreground = App.Current.URController.IsUniversalRobotConnected ? App.Current.ThemeColor : Brushes.Tomato;

                RobotStatusLabel.Content = App.Current.URController.IsUniversalRobotConnected ?
                UniversalRobotHelperFunctions.RobotModeToString(App.Current.URController.URRobotStatus.RobotMode)
                :
                "...";

                //Polaris information
                PolarisLabelStatus.Content = App.Current.PolarisController.IsPolarisConnected ? "Connected" : "Connecting...";
                PolarisLabelStatus.Foreground = App.Current.PolarisController.IsPolarisConnected ? App.Current.ThemeColor : Brushes.Tomato;

                PolarisCameraController.PolarisRigidBody rBody = new PolarisCameraController.PolarisRigidBody();
                if (ComboBoxPolarisSelection.SelectedIndex == 0)
                {
                    rBody = App.Current.PolarisController.GetURRobotRigidBody();
                }
                else if (ComboBoxPolarisSelection.SelectedIndex == 1)
                {
                    rBody = App.Current.PolarisController.GetUserRigidBodyOne();
                }
                else if (ComboBoxPolarisSelection.SelectedIndex == 2)
                {
                    rBody = App.Current.PolarisController.GetUserRigidBodyTwo();
                }

                PolarisX.Text = rBody.x.ToString();
                PolarisY.Text = rBody.y.ToString();
                PolarisZ.Text = rBody.z.ToString();
                //PolarisQX.Text = rBody.qx.ToString();
                //PolarisQY.Text = rBody.qy.ToString();
                //PolarisQZ.Text = rBody.qz.ToString();
                //PolarisQO.Text = rBody.qo.ToString();
                Vector3D rpyFromQ = App.Current.CoordinateTranslator.QuaternionToRPY(rBody.qx,
                    rBody.qy, rBody.qz, rBody.qo);

                PolarisQX.Text = radtoang(rpyFromQ.X).ToString();
                PolarisQY.Text = radtoang(rpyFromQ.Y).ToString();
                PolarisQZ.Text = radtoang(rpyFromQ.Z).ToString();

                AnalogRead.Text = UniversalRobotHelperFunctions.ConvertAnalogReaderToMM(App.Current.URSecondController.GetAnalogValue()).ToString();

                safetyStatus.Text = App.Current.URSecondController.GetCurrentSafetyStatus().ToString();

            }));
        }

        bool VerifyEStop()
        {
            EStopVerify verifyEStop = new EStopVerify();
            verifyEStop.Owner = this;
            verifyEStop.ShowDialog();
            if (verifyEStop.DialogResult != true)
            {
                return false;
            }
            return true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            URDebugWindow window = new URDebugWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void AlignButton_Click(object sender, RoutedEventArgs e)
        {
            AlignWindow window = new AlignWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO (9/4/2019): revisit this check when we have estop
            //if (VerifyEStop())
            {
                CalibrateWindow window = new CalibrateWindow();
                window.Owner = this;
                window.ShowDialog();
            }
        }

        private void TrackButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO (9/4/2019): revisit this check when we have estop
            //if (VerifyEStop())
            {
                TrackWindow window = new TrackWindow();
                window.Owner = this;
                window.ShowDialog();
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            HomeWindow window = new HomeWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow window = new SettingsWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (App.Current.URController.URRobotStatus.RobotMode != 7)
            {
                App.Current.URNoPendantControl.PowerOn();
            }
            else
            {
                App.Current.URNoPendantControl.PowerOff();
            }
        }

        private void TareForce_Click(object sender, RoutedEventArgs e)
        {
            UniversalRobotHelperFunctions.TareForceSensor();
        }

        private void CalibrateButton_V2_Click(object sender, RoutedEventArgs e)
        {
            //TODO (9/4/2019): revisit this check when we have estop
            //if (VerifyEStop())
            {
                CalibrationManualWindow window = new CalibrationManualWindow();
                window.Owner = this;
                window.ShowDialog();
            }
        }

        private void SaveLocationButton_Click(object sender, RoutedEventArgs e)
        {
            string output = GetCurrentLocationString();
            Microsoft.Win32.SaveFileDialog saveFile = new Microsoft.Win32.SaveFileDialog();
            saveFile.FileName = "PatientLocation.txt";
            saveFile.DefaultExt = ".txt";
            saveFile.Filter = "Text Documents (.txt) |*.txt";
            if (saveFile.ShowDialog() == true)
            {
                File.WriteAllText(saveFile.FileName, output);
            }
        }

        private void LoadLocationButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFile = new Microsoft.Win32.OpenFileDialog();
            openFile.DefaultExt = ".txt";
            openFile.Filter = "Text Documents (.txt) |*.txt";
            if (openFile.ShowDialog() == true)
            {
                if (MessageBox.Show("The Robot will now move to the saved location. Do you want to continue with the move?", "Allow move?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    LoadLocationFile(File.ReadAllText(openFile.FileName));
                }
            }
        }

        string GetCurrentLocationString()
        {
            double xPos = App.Current.URController.URRobotStatus.ToolVectorActual_1;
            double yPos = App.Current.URController.URRobotStatus.ToolVectorActual_2;
            double zPos = App.Current.URController.URRobotStatus.ToolVectorActual_3;
            double xRot = App.Current.URController.URRobotStatus.ToolVectorActual_4;
            double yRot = App.Current.URController.URRobotStatus.ToolVectorActual_5;
            double zRot = App.Current.URController.URRobotStatus.ToolVectorActual_6;
            string output = "";
            output += xPos.ToString();
            output += ",";
            output += yPos.ToString();
            output += ",";
            output += zPos.ToString();
            output += ",";
            output += xRot.ToString();
            output += ",";
            output += yRot.ToString();
            output += ",";
            output += zRot.ToString();
            return output;
        }

        void LoadLocationFile(string location)
        {
            string[] contents = location.Split(',');
            if (contents.Count() != 6)
            {
                return;
            }
            double xPos = Convert.ToDouble(contents[0]);
            double yPos = Convert.ToDouble(contents[1]);
            double zPos = Convert.ToDouble(contents[2]);
            double xRot = Convert.ToDouble(contents[3]);
            double yRot = Convert.ToDouble(contents[4]);
            double zRot = Convert.ToDouble(contents[5]);
            //App.Current.URController.UpdateRobotCoordinate(
            //           xPos,
            //           yPos,
            //           zPos,
            //           xRot,
            //           yRot,
            //           zRot,
            //           true);
            App.Current.URController.MoveToSavedPosition(
                        xPos,
                       yPos,
                       zPos,
                       xRot,
                       yRot,
                       zRot);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            App.Current.URController.SetVirtualEStopOverride(false);
        }
    }
}
