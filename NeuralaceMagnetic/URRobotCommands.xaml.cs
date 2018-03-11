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
    /// Interaction logic for URRobotCommands.xaml
    /// </summary>
    public partial class URRobotCommands : Window
    {
        private DispatcherTimer uiTimer;
        public URRobotCommands()
        {
            InitializeComponent();
            CreateUIUpdateThread();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            uiTimer.Stop();
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
                statusLabel.Text = App.Current.URNoPendantControl.GetLastStatus();
            }));
        }

        private void PowerOnButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URNoPendantControl.PowerOn();
        }

        private void PowerOffButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URNoPendantControl.PowerOff();
        }

        private void BrakeReleaseButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URNoPendantControl.BrakeRelease();
        }

        private void CloseSafetyPopup_Click(object sender, RoutedEventArgs e)
        {
            App.Current.URNoPendantControl.CloseSafetyPopup();
        }
    }
}
