using NeuralaceMagnetic.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for HomeWindow.xaml
    /// </summary>
    public partial class HomeWindow : Window
    {
        const double HOME_POSITION = 1.5708;
        const double NEGATIVE_HOME_POSITION = -1.5708;

        double radianAngleFound = 0;
        double radianZAngleFound = 0;
        bool homingStarted = false;

        public HomeWindow()
        {
            InitializeComponent();
            App.Current.URController.PropertyChanged += URController_PropertyChanged;
        }

        private void URController_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ProgramState" /*&& App.Current.URController.URRobotStatus.ProgramState == 1*/ && homingStarted)
            {
                homingStarted = false;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action) (() => 
                {
                    CalibrateCameraWithRobot();
                }));
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void MoveRobotToHome_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MachineHomed = false;

            if (Math.Round(App.Current.URController.URRobotStatus.Q_Actual_1, 4) == HOME_POSITION &&
                Math.Round(App.Current.URController.URRobotStatus.Q_Actual_3, 4) == HOME_POSITION &&
                Math.Round(App.Current.URController.URRobotStatus.Q_Actual_2, 4) == NEGATIVE_HOME_POSITION &&
                Math.Round(App.Current.URController.URRobotStatus.Q_Actual_4, 4) == NEGATIVE_HOME_POSITION)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    CalibrateCameraWithRobot();
                }));
            }
            else
            {
                App.Current.URController.MoveToHomePostition();
                MoveRobotToHome.IsEnabled = false;
                homingStarted = true;
            }
            
        }

        double RadianToAngle(double rad)
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

        private double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private void CalibrateCameraWithRobot_Click(object sender, RoutedEventArgs e)
        {
            CalibrateCameraWithRobot();
        }

        private void CalibrateCameraWithRobot()
        {
            bool angleFound = false;
            for (int i = 0; i < 10; i++)
            {
                if (App.Current.PolarisController.GetRigidBody(Controls.PolarisCameraController.RigidBodyIndex.Camera).isInRange)
                {

                    angleFound = true;
                    PolarisCameraController.PolarisRigidBody rBody = App.Current.PolarisController.GetRigidBody(Controls.PolarisCameraController.RigidBodyIndex.Camera);
                    Vector3D rpyFromQ = App.Current.CoordinateTranslator.QuaternionToRPY(rBody.qx, rBody.qy, rBody.qz, rBody.qo);
                    radianAngleFound = -rpyFromQ.Y;
                    radianZAngleFound = -(rpyFromQ.X + 1.571);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
            if (angleFound)
            {
                double degreeFound = RadianToAngle(radianAngleFound);
                double zDegreeFound = RadianToAngle(radianZAngleFound);

                App.Current.MachineHomed = true;
                App.Current.CoordinateTranslator.SetBaseRotation(radianAngleFound, radianZAngleFound);// zWithBias);
            }
            else
            {
                MessageBox.Show("The angle could not be found please check the calibration area and camera setup.",
                    "Angle not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            this.DialogResult = false;
            this.Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            App.Current.URController.PropertyChanged -= URController_PropertyChanged;
        }
    }
}
