using NeuralaceMagnetic.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
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
    /// Interaction logic for CalibrateWindow.xaml
    /// </summary>
    public partial class CalibrateWindow : Window
    {
        bool hasStartBeenSet = false;
        double startX;
        double startY;
        double startZ;
        double startrX;
        double startrY;
        double startrZ;
        Point3D initialPoint;
        Point3D startPoint;
        Vector3D lookDirection;
        int passNumber = 1;

        //Run through thread controls
        bool cancelRunThroughAll = false;
        bool isRunningThroughAll = false;

        bool isInZCalibration = false;
        bool doCalibration = true;

        bool triggerStimulation = true;
        bool doZTracking = false;
        private DispatcherTimer uiTimer;
        double coilToTargetDistance;

        public CalibrateWindow()
        {
            InitializeComponent();
            coilToTargetDistance = App.Current.ApplicationSettings.CoilToTargetDistanceMM;
            ZeroOutReponses();
            UniversalRobotHelperFunctions.TareForceSensor(); 
            CreateUIUpdateThread();
            UpdateLabels(1, App.Current.ApplicationSettings.TherapyPassOneJumpMM);
            SetStartingPoint();
            if (App.Current.ApplicationSettings.TrackTOFSensor)
            {
                string returnValue = Microsoft.VisualBasic.Interaction.InputBox("Enter the Coil to Target distance", "Coil to Target distance entry", App.Current.ApplicationSettings.CoilToTargetDistanceMM.ToString());
                
                try
                {
                    coilToTargetDistance = Convert.ToDouble(returnValue);
                    if (coilToTargetDistance <= 0)
                    {
                        coilToTargetDistance = App.Current.ApplicationSettings.CoilToTargetDistanceMM;
                    }
                }
                catch { }
                if (MessageBox.Show("The calibration will now begin and the tool will move to a Coil to Target distance of " + coilToTargetDistance + "mm."
                    + "\nWould you like to continue with the calibration?",
                    "Perform Calibration?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question)
                    != MessageBoxResult.Yes)
                {
                    doCalibration = false;
                }
                else
                {
                    MoveDownToZAndSetPoint(startPoint);
                }
            }
            else
            {
                checkBoxTrackZ.IsEnabled = false;
                initialPoint = startPoint;
            }
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
            bool virtualEStopPressed = App.Current.URController.IsVirtualEStopPressed();
            if (virtualEStopPressed || isForceOverLimit)
            {
                if (isRunningThroughAll)
                {
                    cancelRunThroughAll = true;
                }
            }

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

        void SetXYZ()
        {
            startX = App.Current.URController.URRobotStatus.ToolVectorActual_1;
            startY = App.Current.URController.URRobotStatus.ToolVectorActual_2;
            startZ = App.Current.URController.URRobotStatus.ToolVectorActual_3;
            startPoint = new Point3D(startX, startY, startZ);
        }

        Point3D GetCurrentPoint()
        {
            NeuralaceMagnetic.Controls.UniversalRobotController.URRobotCoOrdinate coord = App.Current.URController.GetCurrentLocation();
            return new Point3D(coord.x, coord.y, coord.z);
        }

        private void SetStartingPoint()
        {
            UpdateAllButtonLocations(5);

            hasStartBeenSet = true;

            SetXYZ();

            //Vector3D latestAngles = App.Current.URSecondController.GetLatestAngleInfo();

            //startrX = latestAngles.X;
            //startrY = latestAngles.Y;
            //startrZ = latestAngles.Z;
            startrX = App.Current.URController.URRobotStatus.ToolVectorActual_4;
            startrY = App.Current.URController.URRobotStatus.ToolVectorActual_5;
            startrZ = App.Current.URController.URRobotStatus.ToolVectorActual_6;

            lookDirection = new Vector3D(
                startrX,
                startrY,
                startrZ);
        }

        Point3D GetPoint(Point3D startPoint, Controls.CameraURCoordinateTranslator.Direction eDirection)
        {
            Vector3D copy = lookDirection;
            Point3D translatedPoint = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(
                startPoint,
                copy,
                GetDistanceToMove(),
                eDirection);

            return translatedPoint;
        }

        Point3D TrackZ(Point3D inputPoint, double mmToAddToDistance)
        {
            double analogConverted = UniversalRobotHelperFunctions.GetCurrentZDistance();

            double currentZDistance = coilToTargetDistance + mmToAddToDistance;
            if (double.IsNaN(analogConverted) ||
                double.IsNaN(currentZDistance))
            {
                MessageBox.Show("Cannot read a valid distance from the laser. Unable to perform move!", "Unable to perform move!", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Point3D(
                    App.Current.URController.URRobotStatus.ToolVectorActual_1,
                    App.Current.URController.URRobotStatus.ToolVectorActual_2,
                    App.Current.URController.URRobotStatus.ToolVectorActual_3);
            }
            return UniversalRobotHelperFunctions.AdjustLocationToLaser(inputPoint, lookDirection, analogConverted, currentZDistance);
        }

        bool CheckRunThroughAll()
        {
            if (isRunningThroughAll)
            {
                cancelRunThroughAll = true;
            }
            return isRunningThroughAll;
        }

        Point3D TrackZAndUpdateStartPoint(Point3D inputPoint, double mmToAddToDistance)
        {
            double analogConverted = UniversalRobotHelperFunctions.GetCurrentZDistance();

            double currentZDistance = coilToTargetDistance + mmToAddToDistance;
            if (double.IsNaN(analogConverted) ||
                double.IsNaN(currentZDistance))
            {
                string errorMessage = "Cannot read a valid distance from the laser. Unable to perform move!";
                if (CheckRunThroughAll())
                {
                    errorMessage += " The Run Through All moves will now be cancelled.";
                }
                MessageBox.Show(errorMessage, "Unable to perform move!", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Point3D(
                    App.Current.URController.URRobotStatus.ToolVectorActual_1,
                    App.Current.URController.URRobotStatus.ToolVectorActual_2,
                    App.Current.URController.URRobotStatus.ToolVectorActual_3);
            }

            if (analogConverted < currentZDistance)
            {
                startPoint = UniversalRobotHelperFunctions.AdjustLocationToLaser(startPoint, lookDirection, analogConverted, currentZDistance);
            } 
            return UniversalRobotHelperFunctions.AdjustLocationToLaser(inputPoint, lookDirection, analogConverted, currentZDistance);
        }

        Point3D MoveZ(Point3D inputPoint, double mmToAddToDistance)
        {
            Vector3D copy = lookDirection;
            Point3D translatedPoint = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(
                inputPoint,
                copy,
                (mmToAddToDistance / 1000),
                CameraURCoordinateTranslator.Direction.Backward);
            return translatedPoint;
        }

        void MoveDownToZAndSetPoint(Point3D pointToMove)
        {
            pointToMove = TrackZ(pointToMove, 0);
            startPoint = pointToMove;
            initialPoint = startPoint;
            MoveToPoint(startPoint, true);
        }

        void MoveToPoint(Point3D pointToMove, bool donottriggerstim = false, bool trackz = true, bool slowMoveSpeed = false)
        {
            Thread.Sleep(Convert.ToInt16(App.Current.ApplicationSettings.MaxTrackingTimeWindowMS + 300));

            App.Current.URController.UpdateRobotCoordinate(
                       pointToMove.X,
                       pointToMove.Y,
                       pointToMove.Z,
                       startrX,
                       startrY,
                       startrZ);

            if (doZTracking && trackz)
            {
                Thread.Sleep(500);

                pointToMove = TrackZAndUpdateStartPoint(pointToMove, 0);

                App.Current.URController.UpdateRobotCoordinate(
                       pointToMove.X,
                       pointToMove.Y,
                       pointToMove.Z,
                       startrX,
                       startrY,
                       startrZ);
            }

            if (triggerStimulation && !donottriggerstim)
            {
                App.Current.URController.FireDigitalOutputWhenPositionIsReached();
            }
        }

        double GetZDistanceMove()
        {
            return 1 / 1000;
        }

        double GetDistanceToMove()
        {
            if (passNumber == 1)
            {
                return (App.Current.ApplicationSettings.TherapyPassOneJumpMM / 1000);
            }
            else if (passNumber == 2)
            {
                return (App.Current.ApplicationSettings.TherapyPassTwoJumpMM / 1000);
            }
            else
            {
                return GetZDistanceMove();
            }
            return 0;
        }

        void GotoOneZ()
        {
            UpdateAllButtonLocations(1);

            Point3D outPoint = MoveZ(startPoint, 0);
            MoveToPoint(outPoint, false, false);
        }

        void GotoTwoZ()
        {
            UpdateAllButtonLocations(2);

            Point3D outPoint = MoveZ(startPoint, 1);
            MoveToPoint(outPoint, false, false);
        }

        void GotoThreeZ()
        {
            UpdateAllButtonLocations(3);

            Point3D outPoint = MoveZ(startPoint, 2);
            MoveToPoint(outPoint, false, false);
        }

        void GotoFourZ()
        {
            UpdateAllButtonLocations(4);

            Point3D outPoint = MoveZ(startPoint, 3);
            MoveToPoint(outPoint, false, false);
        }

        void GotoFiveZ()
        {
            UpdateAllButtonLocations(5);

            Point3D outPoint = MoveZ(startPoint, 4);
            MoveToPoint(outPoint, false, false);
        }

        void GotoFiveZWithNoStim()
        {
            UpdateAllButtonLocations(5);

            Point3D outPoint = MoveZ(startPoint, 4);
            MoveToPoint(outPoint, true, false);
        }

        void GotoOne()
        {
            UpdateAllButtonLocations(1);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Left);
            Point3D outPoint2 = GetPoint(outPoint, Controls.CameraURCoordinateTranslator.Direction.Up);            
            MoveToPoint(outPoint2);            
        }

        void GotoTwo()
        {
            UpdateAllButtonLocations(2);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Up);
            MoveToPoint(outPoint);
        }

        void GotoThree()
        {
            UpdateAllButtonLocations(3);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Right);
            Point3D outPoint2 = GetPoint(outPoint, Controls.CameraURCoordinateTranslator.Direction.Up);
            MoveToPoint(outPoint2);
        }

        void GotoFour()
        {
            UpdateAllButtonLocations(4);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Left);
            MoveToPoint(outPoint);
        }

        void GotoFive()
        {
            UpdateAllButtonLocations(5);

            MoveToPoint(startPoint);
        }

        void GotoSix()
        {
            UpdateAllButtonLocations(6);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Right);
            MoveToPoint(outPoint);
        }

        void GotoSeven()
        {
            UpdateAllButtonLocations(7);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Left);
            Point3D outPoint2 = GetPoint(outPoint, Controls.CameraURCoordinateTranslator.Direction.Down);
            MoveToPoint(outPoint2);
        }

        void GotoEight()
        {
            UpdateAllButtonLocations(8);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Down);
            MoveToPoint(outPoint);
        }

        void GotoNine()
        {
            UpdateAllButtonLocations(9);

            Point3D outPoint = GetPoint(startPoint, Controls.CameraURCoordinateTranslator.Direction.Right);
            Point3D outPoint2 = GetPoint(outPoint, Controls.CameraURCoordinateTranslator.Direction.Down);
            MoveToPoint(outPoint2);
        }

        private void SectionOne_Click(object sender, RoutedEventArgs e)
        {
            GotoOne();
        }

        private void SectionTwo_Click(object sender, RoutedEventArgs e)
        {
            GotoTwo();
        }

        private void SectionThree_Click(object sender, RoutedEventArgs e)
        {
            GotoThree();
        }

        private void SectionFour_Click(object sender, RoutedEventArgs e)
        {
            GotoFour();
        }

        private void SectionFive_Click(object sender, RoutedEventArgs e)
        {
            GotoFive();
        }

        private void SectionSix_Click(object sender, RoutedEventArgs e)
        {
            GotoSix();
        }

        private void SectionSeven_Click(object sender, RoutedEventArgs e)
        {
            GotoSeven();
        }

        private void SectionEight_Click(object sender, RoutedEventArgs e)
        {
            GotoEight();
        }

        private void SectionNine_Click(object sender, RoutedEventArgs e)
        {
            GotoNine();
        }

        void UpdateButtonToDefault(ref Button button)
        {
            button.Background = Brushes.White;
            button.Foreground = App.Current.ThemeColor;
        }

        void SetHighlightedButton(ref Button button)
        {
            button.Foreground = Brushes.White;
            button.Background = App.Current.ThemeColor;
        }

        void UpdateResponseToDefault(ref Button button)
        {
            button.BorderBrush = App.Current.ThemeColor;
        }

        void SetHighlightedResponseButton(ref Button button)
        {
            button.BorderBrush = Brushes.Yellow;
        }

        void UpdateXYButtons(int highlightButton)
        {
            UpdateButtonToDefault(ref SectionOne);
            UpdateButtonToDefault(ref SectionTwo);
            UpdateButtonToDefault(ref SectionThree);
            UpdateButtonToDefault(ref SectionFour);
            UpdateButtonToDefault(ref SectionFive);
            UpdateButtonToDefault(ref SectionSix);
            UpdateButtonToDefault(ref SectionSeven);
            UpdateButtonToDefault(ref SectionEight);
            UpdateButtonToDefault(ref SectionNine);

            UpdateResponseToDefault(ref SectionOneResponse);
            UpdateResponseToDefault(ref SectionTwoResponse);
            UpdateResponseToDefault(ref SectionThreeResponse);
            UpdateResponseToDefault(ref SectionFourResponse);
            UpdateResponseToDefault(ref SectionFiveResponse);
            UpdateResponseToDefault(ref SectionSixResponse);
            UpdateResponseToDefault(ref SectionSevenResponse);
            UpdateResponseToDefault(ref SectionEightResponse);
            UpdateResponseToDefault(ref SectionNineResponse);

            switch (highlightButton)
            {
                case 1:
                    SetHighlightedButton(ref SectionOne);
                    SetHighlightedResponseButton(ref SectionOneResponse);
                    break;
                case 2:
                    SetHighlightedButton(ref SectionTwo);
                    SetHighlightedResponseButton(ref SectionTwoResponse);
                    break;
                case 3:
                    SetHighlightedButton(ref SectionThree);
                    SetHighlightedResponseButton(ref SectionThreeResponse);
                    break;
                case 4:
                    SetHighlightedButton(ref SectionFour);
                    SetHighlightedResponseButton(ref SectionFourResponse);
                    break;
                case 5:
                    SetHighlightedButton(ref SectionFive);
                    SetHighlightedResponseButton(ref SectionFiveResponse);
                    break;
                case 6:
                    SetHighlightedButton(ref SectionSix);
                    SetHighlightedResponseButton(ref SectionSixResponse);
                    break;
                case 7:
                    SetHighlightedButton(ref SectionSeven);
                    SetHighlightedResponseButton(ref SectionSevenResponse);
                    break;
                case 8:
                    SetHighlightedButton(ref SectionEight);
                    SetHighlightedResponseButton(ref SectionEightResponse);
                    break;
                case 9:
                    SetHighlightedButton(ref SectionNine);
                    SetHighlightedResponseButton(ref SectionNineResponse);
                    break;
            }
        }

        void UpdateZButtons(int highlightButton)
        {
            //UpdateButtonToDefault(ref SectionZOne);
            //UpdateButtonToDefault(ref SectionZTwo);
            //UpdateButtonToDefault(ref SectionZThree);
            //UpdateButtonToDefault(ref SectionZFour);
            //UpdateButtonToDefault(ref SectionZFive);

            //switch (highlightButton)
            //{
            //    case 1:
            //        SetHighlightedButton(ref SectionZOne);
            //        break;
            //    case 2:
            //        SetHighlightedButton(ref SectionZTwo);
            //        break;
            //    case 3:
            //        SetHighlightedButton(ref SectionZThree);
            //        break;
            //    case 4:
            //        SetHighlightedButton(ref SectionZFour);
            //        break;
            //    case 5:
            //        SetHighlightedButton(ref SectionZFive);
            //        break; ;
            //}
        }

        void UpdateAllButtonLocations(int highlightButton)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                if (isInZCalibration)
                {
                    UpdateZButtons(highlightButton);
                }
                else
                {
                    UpdateXYButtons(highlightButton);
                }
            }));
        }

        void DoZRunThrough()
        {
            for (int i = 1; i <= 5; i++)
            {
                if (cancelRunThroughAll)
                {
                    cancelRunThroughAll = false;
                    break;
                }

                if (i == 1)
                {
                    GotoFiveZ();
                }
                if (i == 2)
                {
                    GotoFourZ();
                }
                if (i == 3)
                {
                    GotoThreeZ();
                }
                if (i == 4)
                {
                    GotoTwoZ();
                }
                if (i == 5)
                {
                    GotoOneZ();
                }
                double msToSleep = App.Current.ApplicationSettings.CalibrationTreatmentTimeS * 1000;
                Thread.Sleep(Convert.ToInt32(msToSleep));
            }
        }

        void DoXYRunThrough()
        {
            for (int i = 1; i <= 9; i++)
            {
                if (cancelRunThroughAll)
                {
                    cancelRunThroughAll = false;
                    break;
                }

                NewMoves(i);
                
                double msToSleep = App.Current.ApplicationSettings.CalibrationTreatmentTimeS * 1000;
                Thread.Sleep(Convert.ToInt32(msToSleep));
            }
        }

        void LegacyMoves(int i)
        {
            if (i == 1)
            {
                GotoOne();
            }
            if (i == 2)
            {
                GotoTwo();
            }
            if (i == 3)
            {
                GotoThree();
            }
            if (i == 4)
            {
                GotoFour();
            }
            if (i == 5)
            {
                GotoFive();
            }
            if (i == 6)
            {
                GotoSix();
            }
            if (i == 7)
            {
                GotoSeven();
            }
            if (i == 8)
            {
                GotoEight();
            }
            if (i == 9)
            {
                GotoNine();
            }
        }

        void NewMoves(int i)
        {
            if (i == 1)
            {
                GotoFive();
            }
            if (i == 2)
            {
                GotoTwo();
            }
            if (i == 3)
            {
                GotoEight();
            }
            if (i == 4)
            {
                GotoFour();
            }
            if (i == 5)
            {
                GotoSix();
            }
            if (i == 6)
            {
                GotoOne();
            }
            if (i == 7)
            {
                GotoThree();
            }
            if (i == 8)
            {
                GotoSeven();
            }
            if (i == 9)
            {
                GotoNine();
            }
        }

        void RunThroughAll()
        {
            isRunningThroughAll = true;

            for (int i = 5; i >= 1; i--)
            {
                if (cancelRunThroughAll)
                {
                    break;
                }

                UpdateRunThroughButtonText(i.ToString(), SystemSounds.Beep);
                Thread.Sleep(1000);
            }

            UpdateRunThroughButtonText(SystemSounds.Exclamation);

            if (isInZCalibration)
            {
                DoZRunThrough();
            }
            else
            {
                DoXYRunThrough();
            }
            
            isRunningThroughAll = false;
            UpdateRunThroughButtonText();
        }

        void UpdateRunThroughButtonText(string text, SystemSound soundToPlay)
        {
            if (soundToPlay != null)
            {
                soundToPlay.Play();
            }
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                RunThrough.Content = text;
            }));
        }

        void UpdateRunThroughButtonText(SystemSound soundToPlay = null)
        {
            if (isRunningThroughAll)
            {
                UpdateRunThroughButtonText("cancel run through", soundToPlay);
            }
            else
            {
                UpdateRunThroughButtonText("run through all", soundToPlay);
            }
        }

        private void RunThrough_Click(object sender, RoutedEventArgs e)
        {
            if (!hasStartBeenSet)
            {
                return;
            }

            if (isRunningThroughAll)
            {
                cancelRunThroughAll = true;
            }
            else
            {
                Thread newThread = new Thread(RunThroughAll);
                newThread.Start();
            }

            UpdateRunThroughButtonText();
        }

        void UpdateLabels(int passNumber, double jumpDistance)
        {
            UpdateAllButtonLocations(5);

            LabelHeader.Content = "Therapy calibration - Pass " + passNumber.ToString() + " (" + jumpDistance.ToString() + "mm)";
            LabelInstructions.Content = "Select the area with the best pain relief and click " + "complete";
            ZeroOutReponses();
        }

        private void RunThrough_Next_Click(object sender, RoutedEventArgs e)
        {
            CheckRunThroughAll();
            UpdateLabels(++passNumber, App.Current.ApplicationSettings.TherapyPassTwoJumpMM);
            RunThrough_Next.IsEnabled = false;
            RunThrough_Next2.IsEnabled = true;
            RunThrough_Complete.IsEnabled = false;
            RunThrough.Visibility = Visibility.Visible;
            SetXYZ();
        }

        private void RunThrough_Next2_Click(object sender, RoutedEventArgs e)
        {
            CheckRunThroughAll();
            isInZCalibration = true;

            UpdateLabels(++passNumber, GetZDistanceMove());
            RunThrough_Next.IsEnabled = false;
            RunThrough_Next2.IsEnabled = false;
            RunThrough_Complete.IsEnabled = true;
            RunThrough.Visibility = Visibility.Collapsed;

            NumPadGrid.Visibility = Visibility.Collapsed;
            ZPadGrid.Visibility = Visibility.Visible;

            SetXYZ();

            //GotoFiveZWithNoStim();
        }

        private void RunThrough_Complete_Click(object sender, RoutedEventArgs e)
        {
            CheckRunThroughAll();
            this.DialogResult = false;
            this.Close();
        }

        private void SectionZOne_Click(object sender, RoutedEventArgs e)
        {
            GotoOneZ();
        }

        private void SectionZTwo_Click(object sender, RoutedEventArgs e)
        {
            GotoTwoZ();
        }

        private void SectionZThree_Click(object sender, RoutedEventArgs e)
        {
            GotoThreeZ();
        }

        private void SectionZFour_Click(object sender, RoutedEventArgs e)
        {
            GotoFourZ();
        }

        private void SectionZFive_Click(object sender, RoutedEventArgs e)
        {
            GotoFiveZ();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!doCalibration)
            {
                this.DialogResult = false;
                this.Close();
            }
            
        }

        private void checkBoxTriggerTherapy_Checked(object sender, RoutedEventArgs e)
        {
            triggerStimulation = true;
        }

        private void checkBoxTriggerTherapy_Unchecked(object sender, RoutedEventArgs e)
        {
            triggerStimulation = false;
        }

        private void resetTherapy_Click(object sender, RoutedEventArgs e)
        {
            if (isRunningThroughAll)
            {
                cancelRunThroughAll = true;
            }
            
            UpdateRunThroughButtonText();

            Thread.Sleep(100);

            passNumber = 1;

            isInZCalibration = false;

            RunThrough_Next.IsEnabled = true;
            RunThrough_Next2.IsEnabled = false;
            RunThrough_Complete.IsEnabled = false;
            RunThrough.Visibility = Visibility.Visible;

            NumPadGrid.Visibility = Visibility.Visible;
            ZPadGrid.Visibility = Visibility.Collapsed;

            UpdateLabels(1, App.Current.ApplicationSettings.TherapyPassOneJumpMM);
            UpdateAllButtonLocations(5);
            startPoint = initialPoint;
            MoveToPoint(startPoint, true);
        }

        private void jogup_Click(object sender, RoutedEventArgs e)
        {
            Point3D outPoint = MoveZ(GetCurrentPoint(), 1);
            MoveToPoint(outPoint, false, false);
        }

        private void Jogdown_Click(object sender, RoutedEventArgs e)
        {
            Point3D outPoint = MoveZ(GetCurrentPoint(), -1);
            MoveToPoint(outPoint, false, false);
        }

        private void checkBoxTrackZ_Checked(object sender, RoutedEventArgs e)
        {
            doZTracking = true;
        }

        private void checkBoxTrackZ_Unchecked(object sender, RoutedEventArgs e)
        {
            doZTracking = false;
        }

        //private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        //{
        //    UpdatePainReponse();
        //}


        //void UpdateSinglePainResponse(ref TextBox textbox, ref Label label, ref Rectangle rect)
        //{
        //    int value = 0;
        //    try
        //    {
        //        value = Convert.ToInt16(textbox.Text);
        //        if (value > 10)
        //        {
        //            value = 10;
        //            textbox.Text = value.ToString();
        //        }
        //        if (value < 0)
        //        {
        //            value = 0;
        //            textbox.Text = value.ToString();
        //        }
        //    }
        //    catch { }
        //    rect.Opacity = (double)value / (double)10;
        //    if (value > 5)
        //    {
        //        label.Foreground = Brushes.White;
        //    }
        //    else
        //    {
        //        label.Foreground = App.Current.ThemeColor;
        //    }
        //}

        void ZeroOutReponses()
        {
            //ResponseOne.Text = "0";
            //ResponseTwo.Text = "0";
            //ResponseThree.Text = "0";
            //ResponseFour.Text = "0";
            //ResponseFive.Text = "0";
            //ResponseSix.Text = "0";
            //ResponseSeven.Text = "0";
            //ResponseEight.Text = "0";
            //ResponseNine.Text = "0";
            //UpdatePainReponse();
            SectionOneResponse.Background = Brushes.White;
            SectionTwoResponse.Background = Brushes.White;
            SectionThreeResponse.Background = Brushes.White;
            SectionFourResponse.Background = Brushes.White;
            SectionFiveResponse.Background = Brushes.White;
            SectionSixResponse.Background = Brushes.White;
            SectionSevenResponse.Background = Brushes.White;
            SectionEightResponse.Background = Brushes.White;
            SectionNineResponse.Background = Brushes.White;

            SectionOneResponse.Foreground = App.Current.ThemeColor;
            SectionTwoResponse.Foreground = App.Current.ThemeColor;
            SectionThreeResponse.Foreground = App.Current.ThemeColor;
            SectionFourResponse.Foreground = App.Current.ThemeColor;
            SectionFiveResponse.Foreground = App.Current.ThemeColor;
            SectionSixResponse.Foreground = App.Current.ThemeColor;
            SectionSevenResponse.Foreground = App.Current.ThemeColor;
            SectionEightResponse.Foreground = App.Current.ThemeColor;
            SectionNineResponse.Foreground = App.Current.ThemeColor;
        }

        void UpdateBoxColor(ref Button box)
        {
            if (box.Background == Brushes.White)
            {
                box.Background = App.Current.ThemeColor;
                box.Foreground = Brushes.White;
            }
            else if (box.Background == App.Current.ThemeColor)
            {
                box.Background = Brushes.LightYellow;
                box.Foreground = App.Current.ThemeColor;
            }
            else
            {
                box.Background = Brushes.White;
                box.Foreground = App.Current.ThemeColor;
            }
            Keyboard.ClearFocus();
        }

        private void SectionOneResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionOneResponse);
        }

        private void SectionTwoResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionTwoResponse);
        }

        private void SectionThreeResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionThreeResponse);
        }

        private void SectionFourResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionFourResponse);
        }

        private void SectionFiveResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionFiveResponse);
        }

        private void SectionSixResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionSixResponse);
        }

        private void SectionSevenResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionSevenResponse);
        }

        private void SectionEightResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionEightResponse);
        }

        private void SectionNineResponse_Click(object sender, RoutedEventArgs e)
        {
            UpdateBoxColor(ref SectionNineResponse);
        }

        //void UpdatePainReponse()
        //{
        //    if (!this.IsLoaded)
        //    {
        //        return;
        //    }
        //    try
        //    {
        //        UpdateSinglePainResponse(ref ResponseOne, ref ResponseLabelOne, ref RectSectionOne);
        //        UpdateSinglePainResponse(ref ResponseTwo, ref ResponseLabelTwo, ref RectSectionTwo);
        //        UpdateSinglePainResponse(ref ResponseThree, ref ResponseLabelThree, ref RectSectionThree);
        //        UpdateSinglePainResponse(ref ResponseFour, ref ResponseLabelFour, ref RectSectionFour);
        //        UpdateSinglePainResponse(ref ResponseFive, ref ResponseLabelFive, ref RectSectionFive);
        //        UpdateSinglePainResponse(ref ResponseSix, ref ResponseLabelSix, ref RectSectionSix);
        //        UpdateSinglePainResponse(ref ResponseSeven, ref ResponseLabelSeven, ref RectSectionSeven);
        //        UpdateSinglePainResponse(ref ResponseEight, ref ResponseLabelEight, ref RectSectionEight);
        //        UpdateSinglePainResponse(ref ResponseNine, ref ResponseLabelNine, ref RectSectionNine);
        //    }
        //    catch { }
        //}

        //private void ResponseOneDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseOne);
        //}

        //private void ResponseTwoDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseTwo);
        //}

        //private void ResponseThreeDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseThree);
        //}

        //private void ResponseFourDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseFour);
        //}

        //private void ResponseFiveDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseFive);
        //}

        //private void ResponseSixDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseSix);
        //}

        //private void ResponseSevenDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseSeven);
        //}

        //private void ResponseEightDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseEight);
        //}

        //private void ResponseNineDec_Click(object sender, RoutedEventArgs e)
        //{
        //    DecrementTextbox(ref ResponseNine);
        //}

        //private void ResponseOneInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseOne);
        //}

        //private void ResponseTwoInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseTwo);
        //}

        //private void ResponseThreeInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseThree);
        //}

        //private void ResponseFourInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseFour);
        //}

        //private void ResponseFiveInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseFive);
        //}

        //private void ResponseSixInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseSix);
        //}

        //private void ResponseSevenInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseSeven);
        //}

        //private void ResponseEightInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseEight);
        //}

        //private void ResponseNineInc_Click(object sender, RoutedEventArgs e)
        //{
        //    IncrementTextBox(ref ResponseNine);
        //}

        //void DecrementTextbox(ref TextBox textBox)
        //{
        //    try 
        //    {
        //        int value = Convert.ToInt16(textBox.Text);
        //        if (value > 0)
        //        {
        //            value--;
        //            textBox.Text = value.ToString();
        //        }
        //    }
        //    catch { }
        //}

        //void IncrementTextBox(ref TextBox textBox)
        //{
        //    try
        //    {
        //        int value = Convert.ToInt16(textBox.Text);
        //        if (value < 10)
        //        {
        //            value++;
        //            textBox.Text = value.ToString();
        //        }
        //    }
        //    catch { }
        //}
    }
}
