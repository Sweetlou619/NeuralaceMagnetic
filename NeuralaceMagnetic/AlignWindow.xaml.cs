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

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for AlignWindow.xaml
    /// </summary>
    public partial class AlignWindow : Window
    {
        public AlignWindow()
        {
            InitializeComponent();
            App.Current.URNoPendantControl.TurnOnFreeDrive();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            App.Current.URNoPendantControl.TurnOffFreeDrive();
        }
    }
}
