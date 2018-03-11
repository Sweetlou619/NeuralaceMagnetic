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
using System.Windows.Shapes;

namespace NeuralaceMagnetic
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        void LoadSettings()
        {
            App.Current.ApplicationSettings.LoadFromFile();
            UpdateUI(App.Current.ApplicationSettings);
        }

        void UpdateUI(ApplicationSettings settings)
        {
            textBoxCalibrationTreatmentTime.Text = settings.CalibrationTreatmentTimeS.ToString();
            textBoxCoilToTargetDistance.Text = settings.CoilToTargetDistanceMM.ToString();
            textBoxFirstPassGridSize.Text = settings.TherapyPassOneJumpMM.ToString();
            textBoxMaxTrackingRegion.Text = settings.MaximumTrackingDistanceMM.ToString();
            textBoxSecondPassGridSize.Text = settings.TherapyPassTwoJumpMM.ToString();
            textBoxURIp.Text = settings.URIpAddress.ToString();
            textBoxCamIp.Text = settings.CameraIpAddress.ToString();
            textBoxMaxTrackingMovePerTime.Text = settings.MaxTrackingMovePerWindowMM.ToString();
            textBoxMaxTrackingMoveTime.Text = settings.MaxTrackingTimeWindowMS.ToString();
            checkBoxTrackTOF.IsChecked = settings.TrackTOFSensor;
            textBoxTofOffset.Text = settings.TOFDistance.ToString();
            textBoxForceSensorIP.Text = settings.ForceSensorIpAddress;
            textBoxForceThreshold.Text = settings.ForceSensorThresholdNewtons.ToString();
            textBoxForceRetractDistance.Text = settings.ForceRetractDistanceMM.ToString();
        }

        bool SaveSettings()
        {
            try
            {
                App.Current.ApplicationSettings.CalibrationTreatmentTimeS = Convert.ToDouble(textBoxCalibrationTreatmentTime.Text);
                App.Current.ApplicationSettings.CoilToTargetDistanceMM = Convert.ToDouble(textBoxCoilToTargetDistance.Text);
                App.Current.ApplicationSettings.TherapyPassOneJumpMM = Convert.ToDouble(textBoxFirstPassGridSize.Text);
                App.Current.ApplicationSettings.MaximumTrackingDistanceMM = Convert.ToDouble(textBoxMaxTrackingRegion.Text);
                App.Current.ApplicationSettings.TherapyPassTwoJumpMM = Convert.ToDouble(textBoxSecondPassGridSize.Text);
                App.Current.ApplicationSettings.URIpAddress = textBoxURIp.Text;
                App.Current.ApplicationSettings.CameraIpAddress = textBoxCamIp.Text;
                App.Current.ApplicationSettings.MaxTrackingMovePerWindowMM = Convert.ToDouble(textBoxMaxTrackingMovePerTime.Text);
                App.Current.ApplicationSettings.MaxTrackingTimeWindowMS = Convert.ToDouble(textBoxMaxTrackingMoveTime.Text);
                App.Current.ApplicationSettings.TrackTOFSensor = checkBoxTrackTOF.IsChecked ?? true;
                App.Current.ApplicationSettings.TOFDistance = Convert.ToDouble(textBoxTofOffset.Text);
                App.Current.ApplicationSettings.ForceSensorIpAddress = textBoxForceSensorIP.Text;
                App.Current.ApplicationSettings.ForceSensorThresholdNewtons = Convert.ToDouble(textBoxForceThreshold.Text);
                App.Current.ApplicationSettings.ForceRetractDistanceMM = Convert.ToDouble(textBoxForceRetractDistance.Text);
            }
            catch
            {
                MessageBox.Show("Error trying to save unknown number.");
                return false;
            }
            App.Current.ApplicationSettings.SaveToFile();
            return true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            bool saved = SaveSettings();
            if (!saved)
            {
                return;
            }
            this.DialogResult = false;
            this.Close();
        }

        private void loadDefaults_Click(object sender, RoutedEventArgs e)
        {            
            UpdateUI(new ApplicationSettings(false));
        }

        private void loadDebug_Click(object sender, RoutedEventArgs e)
        {
            URDebugWindow win = new URDebugWindow();
            win.ShowDialog();
        }
    }
}
