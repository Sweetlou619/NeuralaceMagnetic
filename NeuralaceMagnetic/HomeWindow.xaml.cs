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

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for HomeWindow.xaml
    /// </summary>
    public partial class HomeWindow : Window
    {
        double radianAngleFound = 0;
        double radianZAngleFound = 0;
        public HomeWindow()
        {
            InitializeComponent();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void MoveRobotToHome_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URController.MoveToHomePostition();
            CalibrateCameraWithRobotBtn.IsEnabled = true;
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

                if (MessageBox.Show("The angles " + degreeFound + " and " + zDegreeFound + " were found. Do you want to use these angles for camera calibration?",
                    "Angle set",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information)
                == MessageBoxResult.Yes)
                {
                    App.Current.MachineHomed = true;


                    //double zBias = DegreeToRadian(20);
                    //double zWithBias = 0.001 * radianZAngleFound;

                    App.Current.CoordinateTranslator.SetBaseRotation(radianAngleFound, radianZAngleFound);// zWithBias);
                }
                this.DialogResult = false;
                this.Close();
            }
            else
            {
                MessageBox.Show("The angle could not be found please check the calibration area and camera setup.",
                    "Angle not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
