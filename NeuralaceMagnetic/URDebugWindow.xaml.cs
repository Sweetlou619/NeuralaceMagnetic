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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for URDebugWindow.xaml
    /// </summary>
    public partial class URDebugWindow : Window
    {
        private DispatcherTimer uiTimer;
        Point oldMouse;
        bool isfirstKeyDown = false;
        double initialRobotStartX;
        double initialRobotStartY;
        double initialRobotStartZ;

        public URDebugWindow()
        {
            InitializeComponent();
            CreateUIUpdateThread();
        }

        void CreateUIUpdateThread()
        {
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(100);
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }


        double GetX(Point newMouse, Point oldMouse)
        {
            if (XAxisCombo.SelectedIndex == 0)
            {
                double mouseDiff = newMouse.X - oldMouse.X;
                return initialRobotStartX + (mouseDiff / 5000);
            }
            else if (YAxisCombo.SelectedIndex == 0)
            {
                double mouseDiff = newMouse.Y - oldMouse.Y;
                return initialRobotStartX + (mouseDiff / 5000);
            }
            return initialRobotStartX;
        }

        double GetY(Point newMouse, Point oldMouse)
        {
            if (XAxisCombo.SelectedIndex == 1)
            {
                double mouseDiff = newMouse.X - oldMouse.X;
                return initialRobotStartY + (mouseDiff / 5000);
            }
            else if (YAxisCombo.SelectedIndex == 1)
            {
                double mouseDiff = newMouse.Y - oldMouse.Y;
                return initialRobotStartY + (mouseDiff / 5000);
            }
            return initialRobotStartY;
        }

        double GetZ(Point newMouse, Point oldMouse)
        {
            if (XAxisCombo.SelectedIndex == 2)
            {
                double mouseDiff = newMouse.X - oldMouse.X;
                return initialRobotStartZ + (mouseDiff / 5000);
            }
            else if (YAxisCombo.SelectedIndex == 2)
            {
                double mouseDiff = newMouse.Y - oldMouse.Y;
                return initialRobotStartZ + (mouseDiff / 5000);
            }
            return initialRobotStartZ;
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            Point newMouse = Mouse.GetPosition(this.MouseGrid);

            if (Keyboard.IsKeyDown(Key.LeftShift) && newMouse.X > 0 && newMouse.Y > 0)
            {
                if (!isfirstKeyDown)
                {
                    oldMouse = newMouse;


                    initialRobotStartX = App.Current.URController.URRobotStatus.ToolVectorActual_1;
                    initialRobotStartY = App.Current.URController.URRobotStatus.ToolVectorActual_2;
                    initialRobotStartZ = App.Current.URController.URRobotStatus.ToolVectorActual_3;
                    isfirstKeyDown = true;
                }
                else
                {

                    App.Current.URController.UpdateRobotCoordinate(
                        GetX(newMouse, oldMouse),
                        GetY(newMouse, oldMouse),
                        GetZ(newMouse, oldMouse),
                        App.Current.URController.URRobotStatus.ToolVectorActual_4,
                        App.Current.URController.URRobotStatus.ToolVectorActual_5,
                        App.Current.URController.URRobotStatus.ToolVectorActual_6);
                }
            }
            else
            {
                isfirstKeyDown = false;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            uiTimer.Stop();
        }
    }
}
