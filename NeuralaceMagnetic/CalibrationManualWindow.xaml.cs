using NeuralaceMagnetic.Controls;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for CalibrationManualWindow.xaml
    /// </summary>
    public partial class CalibrationManualWindow : Window
    {
        List<Point3D> historyPositions = new List<Point3D>();
        List<Point3D> angleHistoryPositions = new List<Point3D>();
        private DispatcherTimer uiTimer;

        public CalibrationManualWindow()
        {
            InitializeComponent();
            UniversalRobotHelperFunctions.TareForceSensor();
            CreateUIUpdateThread();
            LoadPositions();
        }

        void LoadPositions()
        {
            AddInfoToHistory("Starting Position");
        }

        void AddInfoToHistory(string label = "")
        {
            double xPos = App.Current.URController.URRobotStatus.ToolVectorActual_1;
            double yPos = App.Current.URController.URRobotStatus.ToolVectorActual_2;
            double zPos = App.Current.URController.URRobotStatus.ToolVectorActual_3;
            double xRot = App.Current.URController.URRobotStatus.ToolVectorActual_4;
            double yRot = App.Current.URController.URRobotStatus.ToolVectorActual_5;
            double zRot = App.Current.URController.URRobotStatus.ToolVectorActual_6;

            if (label == "")
            {
                label += historyPositions.Count.ToString() + ". ";
                label += DateTime.Now.ToShortTimeString();
                label += " X: " + xPos.ToString("0.00");
                label += " Y: " + yPos.ToString("0.00");
                label += " Z: " + zPos.ToString("0.00");
            }
            HistoryBox.Items.Add(new ListBoxItem() { Content = label });
            historyPositions.Add(new Point3D() { X = xPos, Y = yPos, Z = zPos });
            angleHistoryPositions.Add(new Point3D() { X = xRot, Y = yRot, Z = zRot });
        }

        private void JogUp_Click(object sender, RoutedEventArgs e)
        {
            MoveXY(Controls.CameraURCoordinateTranslator.Direction.Up);
        }

        private void JogLeft_Click(object sender, RoutedEventArgs e)
        {
            MoveXY(Controls.CameraURCoordinateTranslator.Direction.Left);
        }

        private void JogRight_Click(object sender, RoutedEventArgs e)
        {
            MoveXY(Controls.CameraURCoordinateTranslator.Direction.Right);
        }

        private void JogDown_Click(object sender, RoutedEventArgs e)
        {
            MoveXY(Controls.CameraURCoordinateTranslator.Direction.Down);
        }

        private void JogUp_Z_Click(object sender, RoutedEventArgs e)
        {
            MoveZ(Controls.CameraURCoordinateTranslator.Direction.Backward);
        }

        private void JogDown_Z_Click(object sender, RoutedEventArgs e)
        {
            MoveZ(Controls.CameraURCoordinateTranslator.Direction.Forward);
        }

        private void Trigger_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.FireDigitalOutput();
        }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MoveToSelected();
        }

        void MoveToSelected()
        {
            if (historyPositions.Count > 0 &&
                HistoryBox.SelectedIndex <= historyPositions.Count &&
                HistoryBox.SelectedIndex >= 0)
            {
                MoveToPoint(historyPositions[HistoryBox.SelectedIndex], angleHistoryPositions[HistoryBox.SelectedIndex], true);
            }
        }

        void MoveToPoint(Point3D pointToMove, Point3D lookDirection, bool useAnglesToMove)
        {
            App.Current.URController.UpdateRobotCoordinate(
                       pointToMove.X,
                       pointToMove.Y,
                       pointToMove.Z,
                       lookDirection.X,
                       lookDirection.Y,
                       lookDirection.Z,
                       useAnglesToMove);
        }

        Point3D GetCurrentPoint()
        {
            NeuralaceMagnetic.Controls.UniversalRobotController.URRobotCoOrdinate coord = App.Current.URController.GetCurrentLocation();
            return new Point3D(coord.x, coord.y, coord.z);
        }

        Vector3D GetCurrentLookDirection()
        {
            NeuralaceMagnetic.Controls.UniversalRobotController.URRobotCoOrdinate coord = App.Current.URController.GetCurrentLocation();
            return new Vector3D(coord.qx, coord.qy, coord.qz);
        }

        Point3D GetCurrentLookDirectionAsPoint()
        {
            NeuralaceMagnetic.Controls.UniversalRobotController.URRobotCoOrdinate coord = App.Current.URController.GetCurrentLocation();
            return new Point3D(coord.qx, coord.qy, coord.qz);
        }

        void MoveZ(CameraURCoordinateTranslator.Direction direction)
        {
            Vector3D copy = GetCurrentLookDirection();
            Point3D translatedPoint = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(
                GetCurrentPoint(),
                copy,
                (GetZDistance() / 1000),
                direction);
            MoveToPoint(translatedPoint, GetCurrentLookDirectionAsPoint(), false);
        }

        void MoveXY(Controls.CameraURCoordinateTranslator.Direction eDirection)
        {
            Vector3D copy = GetCurrentLookDirection();
            Point3D translatedPoint = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(
                GetCurrentPoint(),
                copy,
                (GetXYDistance() / 1000),
                eDirection);
            MoveToPoint(translatedPoint, GetCurrentLookDirectionAsPoint(), false);
        }

        double GetZDistance()
        {
            if (ZMoveDistance.SelectedIndex == 0)
            {
                return 1;
            }
            else if (ZMoveDistance.SelectedIndex == 1)
            {
                return 5;
            }
            return 1;
        }

        double GetXYDistance()
        {
            if (XYMoveDistance.SelectedIndex == 0)
            {
                return 1;
            }
            else if (XYMoveDistance.SelectedIndex == 1)
            {
                return 5;
            }
            else if (XYMoveDistance.SelectedIndex == 2)
            {
                return 10;
            }
            return 1;
        }

        private void MoveTo_Click(object sender, RoutedEventArgs e)
        {
            MoveToSelected();
        }

        private void MoveToZ_Click(object sender, RoutedEventArgs e)
        {
            double analogConverted = UniversalRobotHelperFunctions.GetCurrentZDistance();

            double currentZDistance = App.Current.ApplicationSettings.CoilToTargetDistanceMM;
            if (double.IsNaN(analogConverted) ||
                double.IsNaN(currentZDistance))
            {
                MessageBox.Show("Cannot read a valid distance from the laser. Unable to perform move!", "Unable to perform move!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Point3D move = UniversalRobotHelperFunctions.AdjustLocationToLaser(GetCurrentPoint(), GetCurrentLookDirection(), analogConverted, currentZDistance);
                MoveToPoint(move, GetCurrentLookDirectionAsPoint(), false);
            }
        }

        private void JogAngle1_Neg_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.JogAxis(UniversalRobotController.eJogAxis.X, false);
        }

        private void JogAngle1_Pos_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.JogAxis(UniversalRobotController.eJogAxis.X, true);
        }

        private void JogAngle2_Pos_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.JogAxis(UniversalRobotController.eJogAxis.Y, true);
        }

        private void JogAngle2_Neg_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.JogAxis(UniversalRobotController.eJogAxis.Y, false);
        }

        private void JogAngle3_Pos_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.JogAxis(UniversalRobotController.eJogAxis.Z, true);
        }

        private void JogAngle3_Neg_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.JogAxis(UniversalRobotController.eJogAxis.Z, false);
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
            bool isForceOverLimit = UniversalRobotHelperFunctions.IsForceOverLimit();            
            if (isForceOverLimit)
            {
                bool closeWindow = false;
                App.Current.URController.SetVirtualEStopOverride(true);
                if (MessageBox.Show("The force sensor is over the defined limit! Freedrive has been activated. Freedrive mode will be disable when this dialog closes. Would you like to continue with calibration?",
                "Perform Calibration?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    UniversalRobotHelperFunctions.TareForceSensor();
                }
                else
                {
                    closeWindow = true;
                }
                App.Current.URController.SetVirtualEStopOverride(false);

                if (closeWindow)
                    this.Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            uiTimer.Stop();
        }

        private void CreateSave_Click(object sender, RoutedEventArgs e)
        {
            AddInfoToHistory();
        }
    }
}
