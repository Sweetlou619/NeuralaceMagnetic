using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace NeuralaceMagnetic.Controls
{
    public class UniversalRobotHelperFunctions
    {
        static Point3D TareForceReading = new Point3D(0,0,0);

        public static double ConvertURCoordinateToMM(double urMeter)
        {
            return urMeter * 1000;
        }

        public static Point3D GetForceReadout()
        {
            NeuralaceMagnetic.Controls.ForceTorqueSensorController.ForceReadout forceReadout = App.Current.ForceTorqueSensor.GetForceReadOut();
            Point3D returnPoint = new Point3D(forceReadout.XForce, forceReadout.YForce, forceReadout.ZForce);
            return returnPoint;
        }

        public static void TareForceSensor()
        {
            TareForceReading = UniversalRobotHelperFunctions.GetForceReadout();
        }

        public static bool IsForceOverLimit()
        {
            return IsForceOverLimit(TareForceReading.X, TareForceReading.Y, TareForceReading.Z);
        }

        private static bool IsForceOverLimit(double xOffset, double yOffset, double zOffset)
        {
            double limit = Math.Abs(App.Current.ApplicationSettings.ForceSensorThresholdNewtons);
            NeuralaceMagnetic.Controls.ForceTorqueSensorController.ForceReadout forceReadout = App.Current.ForceTorqueSensor.GetForceReadOut();
            return (Math.Abs(forceReadout.XForce - xOffset) > limit
                || Math.Abs(forceReadout.YForce - yOffset) > limit
                || Math.Abs(forceReadout.ZForce - zOffset) > limit);
        }

        public static Vector3D GetForceDirection()
        {
            NeuralaceMagnetic.Controls.ForceTorqueSensorController.ForceReadout forceReadout = App.Current.ForceTorqueSensor.GetForceReadOut();
            Vector3D returnVector = new Vector3D(forceReadout.XForce - TareForceReading.X, forceReadout.YForce - TareForceReading.Y, forceReadout.ZForce - TareForceReading.Z);
            returnVector.Normalize();
            return returnVector;
        }

        public static double GetCurrentZDistance()
        {
            return ConvertAnalogReaderToMM(
                App.Current.URSecondController.GetAnalogValue());
        }

        public static double LimitAnalogRead(double analog)
        {
            double totalV = 5;// 5.8;

            //the laser reads reverse
            double flippedAnalog = totalV - analog;

            //too much noise
            flippedAnalog = Math.Round(flippedAnalog, 3);

            if (flippedAnalog >= totalV || flippedAnalog <= 0)
            {
                return double.NaN;
            }

            return analog;
        }

        public static double ConvertAnalogReaderToMM(double analog)
        {
            if (double.IsNaN(analog))
            {
                return analog;
            }

            double totalV = 5;// 5.8;

            //the laser reads reverse
            double flippedAnalog = totalV - analog;

            //too much noise
            flippedAnalog = Math.Round(flippedAnalog, 3);

            if (flippedAnalog >= totalV || flippedAnalog <= 0)
            {
                return double.NaN;
            }

            double lowerLimitMM = 100 - 35;//35;
            double upperLimitMM = 100 + 35;//65;
            double mmPerRange = upperLimitMM - lowerLimitMM;
            double mmPerV = mmPerRange / totalV;
            double mmConverted = flippedAnalog * mmPerV;
            double totalMM = mmConverted + lowerLimitMM;

            //the volatage varies which reading a single number lets round
            double roundedMM = Math.Round(totalMM);

            return ConvertToMMFromCoil(roundedMM);
        }

        public static double ConvertToMMFromCoil(double mmreading)
        {
            double coilWidth = App.Current.ApplicationSettings.TOFDistance;//45.88;//35.88;
            return mmreading - coilWidth;
        }

        private static double ConvertForceToMove(double force)
        {
            return (force * App.Current.ApplicationSettings.ForceRetractDistanceMM) / 1000;
        }

        public static Point3D AdjustLocationToForceSensor(Point3D current, Vector3D lookDirection, Vector3D forceSensorDirection)
        {
            CameraURCoordinateTranslator.Direction direction = CameraURCoordinateTranslator.Direction.Forward;
            //Z
            double move = App.Current.ApplicationSettings.ForceRetractDistanceMM / 1000;//ConvertForceToMove(forceSensorDirection.Z);
            //if (move < 0)
            //{
                direction = CameraURCoordinateTranslator.Direction.Backward;
            //    move = Math.Abs(move);
            //}
            //else
            //{
            //    direction = CameraURCoordinateTranslator.Direction.Forward;
            //}
            Point3D returnPointZ = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(current, lookDirection, move, direction);

            ////X
            //move = ConvertForceToMove(forceSensorDirection.X);
            //if (move < 0)
            //{
            //    direction = CameraURCoordinateTranslator.Direction.Left;
            //    move = Math.Abs(move);
            //}
            //else
            //{
            //    direction = CameraURCoordinateTranslator.Direction.Right;
            //}
            //Point3D returnPointXZ = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(returnPointZ, lookDirection, move, direction);
            
            ////Y
            //move = ConvertForceToMove(forceSensorDirection.Y);
            //if (move < 0)
            //{
            //    direction = CameraURCoordinateTranslator.Direction.Down;
            //    move = Math.Abs(move);
            //}
            //else
            //{
            //    direction = CameraURCoordinateTranslator.Direction.Up;
            //}
            //Point3D returnPointXYZ = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(returnPointXZ, lookDirection, move, direction);

            //return returnPointXYZ;
            return returnPointZ;
        }

        public static Point3D AdjustLocationToLaser(Point3D current, Vector3D lookDirection, double currentDistanceMM, double tarGetZDistanceMM, double biasMovement = 1)
        {
            if (double.IsNaN(currentDistanceMM) ||
                double.IsNaN(tarGetZDistanceMM))
            {
                return current;
            }

            double zMoveMM = Math.Round((currentDistanceMM - tarGetZDistanceMM), 1);
            double zMove = zMoveMM / 1000;
            if (zMove == 0
                || Math.Abs(zMoveMM) < 0.25)
            {
                return current;
            }

            zMove = zMove * biasMovement;
            CameraURCoordinateTranslator.Direction direction = CameraURCoordinateTranslator.Direction.Forward;
            if (zMove < 0)
            {
                direction = CameraURCoordinateTranslator.Direction.Backward;
                zMove = Math.Abs(zMove);
            }
            else
            {
                direction = CameraURCoordinateTranslator.Direction.Forward;
            }

            Point3D returnPoint = App.Current.CoordinateTranslator.GetPointRelativeToolRoation(current, lookDirection, zMove, direction);
            return returnPoint;
        }
        
        public static string RobotModeToString(double robotMode)
        {
            try
            {
                int mode = Convert.ToInt16(robotMode);
                if (mode == 0)
                {
                    return "DISCONNECTED";
                }
                else if (mode == 1)
                {
                    return "CONFIRM_SAFETY";
                }
                else if (mode == 2)
                {
                    return "BOOTING";
                }
                else if (mode == 3)
                {
                    return "POWER_OFF";
                }
                else if (mode == 4)
                {
                    return "POWER_ON";
                }
                else if (mode == 5)
                {
                    return "IDLE";
                }
                else if (mode == 6)
                {
                    return "BACKDRIVE";
                }
                else if (mode == 7)
                {
                    return "RUNNING";
                }
                else if (mode == 8)
                {
                    return "UPDATING_FIRMWARE";
                }
            }
            catch { }
            return "UNKNOWN";
        }
    }
}
