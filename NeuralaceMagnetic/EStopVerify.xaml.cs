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
    /// Interaction logic for EStopVerify.xaml
    /// </summary>
    public partial class EStopVerify : Window
    {
        private DispatcherTimer uiTimer;

        public EStopVerify()
        {
            InitializeComponent();
            CreateUIUpdateThread();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (App.Current.URController.IsVirtualEStopPressed())
            {
                App.Current.URController.SetVirtualEStopOverride(false);
                this.DialogResult = true;
            }
        }

        void CreateUIUpdateThread()
        {
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(100);
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            uiTimer.Stop();
        }
    }
}
