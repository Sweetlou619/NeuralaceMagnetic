using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace NeuralaceMagnetic.Controls
{
    class TrackCameraWithRobot : BackgroundWorker
    {

        private bool dotracking = false;
        private UniversalRobotController urController;
        private URSecondaryController urController2;
        private PolarisCameraController polarisController;
        private CameraURCoordinateTranslator coordTrans;
        private ApplicationSettings appSettings;
        private TorqueSensorTracking torqueSensorTracker;
        private RigidBodyOptions currentRigidBody = RigidBodyOptions.One;

        bool hasDeltaSetpointsBeenSet = false;
        PolarisCameraController.PolarisRigidBody rigidBodySetpoints = new PolarisCameraController.PolarisRigidBody();
        UniversalRobotController.URRobotCoOrdinate universalRobotInitialSetpoint = new UniversalRobotController.URRobotCoOrdinate();
        UniversalRobotController.URRobotCoOrdinate universalRobotCameraSetpoint = new UniversalRobotController.URRobotCoOrdinate();
        double initialLaserReadMM = double.NaN;
        //double lastLaserRead = double.NaN;
        DateTime lastTimeZWasApplied = DateTime.MinValue;
        DateTime lastTimeZWasMoved = DateTime.MinValue;
        public UniversalRobotController.URRobotCoOrdinate CurrentSetPoint = new UniversalRobotController.URRobotCoOrdinate();
        public string LastErrorMessage = "";
        public bool ErrorHasOccurred = false;
        RigidBodyDeltas lastCycleDeltas = new RigidBodyDeltas();
        bool hasURReachedCameraSetpoint = false;

        static int readPoolCount = 5;
        double[] lastAverage = new double[readPoolCount];
        bool hasInitLaserAverage = false;

        struct RigidBodyDeltas
        {
            public double xDelta;
            public double yDelta;
            public double zDelta;
        }

        enum RigidBodyOptions
        {
            One = 1,
            Two = 2
        }

        public TrackCameraWithRobot(
            UniversalRobotController universalRobotController,
            URSecondaryController secondaryController,
            PolarisCameraController polarisCameraController,
            ApplicationSettings applicationSettings,
            CameraURCoordinateTranslator cameraTranslator,
            TorqueSensorTracking torqueSensor
           )
        {
            urController = universalRobotController;
            polarisController = polarisCameraController;
            coordTrans = cameraTranslator;
            appSettings = applicationSettings;
            urController2 = secondaryController;
            torqueSensorTracker = torqueSensor;
        }

        public double GetLaserSetPoint()
        {
            return initialLaserReadMM;
        }

        public void Start()
        {
            dotracking = true;
            this.RunWorkerAsync();
        }

        public void Stop()
        {
            dotracking = false;
            if (this.IsBusy == true)
            {
                //this.CancelAsync();
            }
        }

        void CheckIfPointIsOutOfRange(Point3D setpoint)
        {
            double maxX = universalRobotInitialSetpoint.x + (appSettings.MaximumTrackingDistanceMM / 1000);
            double maxY = universalRobotInitialSetpoint.y + (appSettings.MaximumTrackingDistanceMM / 1000);
            double maxZ = universalRobotInitialSetpoint.z + (appSettings.MaximumTrackingDistanceMM / 1000);
            double minX = universalRobotInitialSetpoint.x - (appSettings.MaximumTrackingDistanceMM / 1000);
            double minY = universalRobotInitialSetpoint.y - (appSettings.MaximumTrackingDistanceMM / 1000);
            double minZ = universalRobotInitialSetpoint.z - (appSettings.MaximumTrackingDistanceMM / 1000);
            if (setpoint.X > maxX ||
                setpoint.Y > maxY ||
                setpoint.Z > maxZ ||
                setpoint.X < minX ||
                setpoint.Y < minY ||
                setpoint.Z < minZ)
            {
                urController.StopRobotMove();
                throw new Exception("The robot has moved out of the defined range of " + appSettings.MaximumTrackingDistanceMM + "mm.");
            }
        }

        void SwitchToBackupRigid()
        {
            if (currentRigidBody == RigidBodyOptions.One)
            {
                currentRigidBody = RigidBodyOptions.Two;
            }
            else if (currentRigidBody == RigidBodyOptions.Two)
            {
                currentRigidBody = RigidBodyOptions.One;
            }
            hasDeltaSetpointsBeenSet = false;
        }

        void ResetDeltaFlag()
        {
            hasDeltaSetpointsBeenSet = false;
        }

        void SetCameraSetpoints()
        {
            rigidBodySetpoints = new PolarisCameraController.PolarisRigidBody();
            if (currentRigidBody == RigidBodyOptions.One)
            {
                rigidBodySetpoints = polarisController.GetUserRigidBodyOne();
            }
            else if (currentRigidBody == RigidBodyOptions.Two)
            {
                rigidBodySetpoints = polarisController.GetUserRigidBodyTwo();
            }

            universalRobotCameraSetpoint = urController.GetCurrentLocation();
            hasDeltaSetpointsBeenSet = true;
        }

        RigidBodyDeltas ConvertRigidBodyIntoURSpace(RigidBodyDeltas polaris)
        {
            //positive x is toward the camera
            //positive y is to the right of the camera (camera pov)
            //camera z is out toward the robot 
            //z -950 at closent limit to cam, 2400 is outer limit
            //camera x is up negative up
            //camera y is left and right positive right

            //on rigid bodies ndi must be on top

            //camera y positive is left and right
            RigidBodyDeltas converted = new RigidBodyDeltas();
            converted.xDelta = (polaris.zDelta / 1000);
            converted.yDelta = (polaris.yDelta / 1000);
            converted.zDelta = (polaris.xDelta / 1000);// ((-polaris.xDelta) / 1000);

            return converted;
        }

        RigidBodyDeltas GetRigidBodyDeltas()
        {
            RigidBodyDeltas delta = new RigidBodyDeltas();
            PolarisCameraController.PolarisRigidBody currentLocations = new PolarisCameraController.PolarisRigidBody();
            if (currentRigidBody == RigidBodyOptions.One)
            {
                currentLocations = polarisController.GetUserRigidBodyOne();
            }
            else if (currentRigidBody == RigidBodyOptions.Two)
            {
                currentLocations = polarisController.GetUserRigidBodyTwo();
            }
            Console.WriteLine("x:" + currentLocations.x.ToString() +
                              ", y:" + currentLocations.y.ToString() +
                              ", z:" + currentLocations.z.ToString() +
                              ", avg: " + currentLocations.numberOfAverages.ToString());
            delta.xDelta = currentLocations.x - rigidBodySetpoints.x;
            delta.yDelta = currentLocations.y - rigidBodySetpoints.y;
            delta.zDelta = currentLocations.z - rigidBodySetpoints.z;
            return ConvertRigidBodyIntoURSpace(delta);
        }

        RigidBodyDeltas GetCameraRigidBody()
        {
            PolarisCameraController.RigidBodyIndex currentRigidBodyIndex = PolarisCameraController.RigidBodyIndex.UNKNOWN;
            PolarisCameraController.RigidBodyIndex backupRigidBodyIndex = PolarisCameraController.RigidBodyIndex.UNKNOWN;

            if (currentRigidBody == RigidBodyOptions.One)
            {
                currentRigidBodyIndex = PolarisCameraController.RigidBodyIndex.UserOne;
                backupRigidBodyIndex = PolarisCameraController.RigidBodyIndex.UserTwo;
            }
            else if (currentRigidBody == RigidBodyOptions.Two)
            {
                currentRigidBodyIndex = PolarisCameraController.RigidBodyIndex.UserTwo;
                backupRigidBodyIndex = PolarisCameraController.RigidBodyIndex.UserOne;
            }

            int loopCount = 0;
            while (true)
            {
                if (!polarisController.GetRigidBody(currentRigidBodyIndex).isInRange)
                {
                    if (polarisController.GetRigidBody(backupRigidBodyIndex).isInRange)
                    {
                        SwitchToBackupRigid();
                        break;
                    }
                    else
                    {
                        //give the trackers a chance to come back into view
                        if (loopCount > 29)
                        {
                            throw new Exception("All rigid bodies are out of the field of view!");
                        }
                        else
                        {
                            loopCount++;
                            CheckForceSensor();
                            Thread.Sleep(100);
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            if (!hasDeltaSetpointsBeenSet)
            {
                SetCameraSetpoints();
            }

            return GetRigidBodyDeltas();
        }

        bool CheckIfZIsTooClose(double currentMM, double targetMM)
        {
            if (currentMM < 1)
            {
                return true;
            }
            return false;

            //double tooCloseLimit = 5;

            //if (double.IsNaN(currentMM) &&
            //    !double.IsNaN(targetMM))
            //{
            //    return true;
            //}

            //if (!double.IsNaN(currentMM) && !double.IsNaN(targetMM))
            //{
            //    double zMoveMM = currentMM - targetMM;
            //    if (zMoveMM < 0)
            //    {
            //        double abs = Math.Abs(zMoveMM);
            //        if (abs > tooCloseLimit)
            //        {
            //            return true;
            //        }
            //    }
            //}
            //return false;
        }

        bool HasRobotReachedCameraSetpoint(UniversalRobotController.URRobotCoOrdinate currentRobotCoord, Point3D setPoint)
        {
            double tolerance = 0.0002;
            if (Math.Abs(Math.Abs(currentRobotCoord.x) - Math.Abs(setPoint.X)) < tolerance &&
                Math.Abs(Math.Abs(currentRobotCoord.y) - Math.Abs(setPoint.Y)) < tolerance &&
                Math.Abs(Math.Abs(currentRobotCoord.z) - Math.Abs(setPoint.Z)) < tolerance)
            {
                return true;
            }
            return false;
        }

        bool IsWithinRangeForLazerTracking(RigidBodyDeltas lastDeltas, RigidBodyDeltas currentDeltas)
        {
            double tolerance = 0.0002;
            if (Math.Abs(Math.Abs(currentDeltas.xDelta) - Math.Abs(lastDeltas.xDelta)) < tolerance &&
                Math.Abs(Math.Abs(currentDeltas.yDelta) - Math.Abs(lastDeltas.yDelta)) < tolerance &&
                Math.Abs(Math.Abs(currentDeltas.zDelta) - Math.Abs(lastDeltas.zDelta)) < tolerance)
            {
                return true;
            }
            return false;
        }

        void InitializeLaserAveragePool(double read)
        {
            hasInitLaserAverage = true;
            for (int i = 0; i < readPoolCount; i++)
                lastAverage[i] = read;
        }

        void AddNewReadToPool(double read)
        {
            for (int i = 0; i < readPoolCount - 1; i++)
                lastAverage[i] = lastAverage[i + 1];
            lastAverage[readPoolCount - 1] = read;
        }

        double GetLaserRead()
        {
            double runningSum = 0;
            for (int i = 0; i < readPoolCount; i++)
            {
                if (double.IsNaN(lastAverage[i]))
                {
                    return double.NaN;
                }
                runningSum += lastAverage[i];
            }
            return runningSum / readPoolCount;
        }

        void CheckEStop()
        {
            if (urController.IsVirtualEStopPressed()
                || urController2.GetCurrentSafetyStatus() != URSecondaryController.SafetyType.Normal)
            {
                throw new Exception("Universal Robot is not in a runable state or the estop has been pressed!");
            }
        }

        void CheckForceSensor()
        {
            if (torqueSensorTracker.IsErrorActive())
            {
                throw new Exception("Universal Robot Force Sensor is over the force threshold!");
            }
        }

        Point3D GetSetpointFromDeltas(RigidBodyDeltas deltas)
        {
            //ignore z moves
            //deltas.zDelta = 0;

            deltas.xDelta = Math.Round(deltas.xDelta, 5);
            deltas.yDelta = Math.Round(deltas.yDelta, 5);
            deltas.zDelta = Math.Round(deltas.zDelta, 5);

            Point3D deltaPoint = new Point3D(deltas.xDelta, deltas.yDelta, deltas.zDelta);
            Point3D deltaWithRotation = coordTrans.GetPointWithBaseRoation(deltaPoint);

            //then we can apply the deltas that were returned
            Point3D setpoint = new Point3D(
                universalRobotCameraSetpoint.x + deltaWithRotation.X,
                universalRobotCameraSetpoint.y + deltaWithRotation.Y,
                universalRobotCameraSetpoint.z + deltaWithRotation.Z
                );
            return setpoint;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            try
            {
                //set the initial location so we can verify the boundarys
                universalRobotInitialSetpoint = urController.GetCurrentLocation();
                initialLaserReadMM = UniversalRobotHelperFunctions.GetCurrentZDistance();
                UniversalRobotHelperFunctions.TareForceSensor();
                App.Current.TorqueSensorTracking.SetForceTracking(true);

                UpdateInitStatus(new EventArgs());

                int msTime = 100;
                DateTime start = new DateTime();
                DateTime stop = new DateTime();
                TimeSpan span = new TimeSpan();
                bool hasRobotReachedPosition = false;
                bool hasStartedTrackingLazer = false;
                universalRobotCameraSetpoint = urController.GetCurrentLocation();
                //Point3D? robotLocationWhenZTrackingStarted = null;
                bool shouldUpdateMoveSetpoint = false;
                while (dotracking)
                {
                    shouldUpdateMoveSetpoint = true;
                    span = stop - start;
                    span = TimeSpan.FromMilliseconds(msTime) - span;
                    if (span <= TimeSpan.FromMilliseconds(0))
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(msTime));
                    }
                    else
                    {
                        Thread.Sleep(span);
                    }
                    start = DateTime.Now;

                    if (!polarisController.IsPolarisConnected)
                    {
                        throw new Exception("Polaris camera disconnected!");
                    }

                    if (!urController.IsUniversalRobotConnected)
                    {
                        throw new Exception("Universal Robot disconnected!");
                    }

                    CheckEStop();
                    CheckForceSensor();

                    //this will find a marker and set the current xyz of robot
                    RigidBodyDeltas deltas = GetCameraRigidBody();
                    //Get the setpoint from the camera
                    Point3D setpoint = GetSetpointFromDeltas(deltas);

                    //end camera tracking
                    UniversalRobotController.URRobotCoOrdinate currentRobotCoord = urController.GetCurrentLocation();

                    //make sure that the robot has reached its position
                    bool isWithingRangeForZMoves = IsWithinRangeForLazerTracking(lastCycleDeltas, deltas);
                    if (!isWithingRangeForZMoves)
                    {
                        hasRobotReachedPosition = false;
                        if (hasStartedTrackingLazer)
                        {
                            ResetDeltaFlag();
                            //this will find a marker and set the current xyz of robot
                            deltas = GetCameraRigidBody();
                            //Get the setpoint from the camera
                            setpoint = GetSetpointFromDeltas(deltas);
                            hasStartedTrackingLazer = false;
                        }

                        //lastCycleDeltas = deltas;
                    }
                    else
                    {
                        //only start doing z tracking once the position has been met by the robot                        
                        bool hasRobotReachedCameraCoOrd = HasRobotReachedCameraSetpoint(currentRobotCoord, setpoint);
                        if (hasRobotReachedPosition ||
                            hasRobotReachedCameraCoOrd)
                        {
                            //we have reached position lock in the bool until the camera markers move again
                            hasRobotReachedPosition = true;
                        }
                    }
                    lastCycleDeltas = deltas;

                    if (this.appSettings.TrackTOFSensor && !double.IsNaN(initialLaserReadMM))
                    {
                        //do laser tracking                    
                        //now that current xyz is set we can adjust for the laser
                        if (App.Current.URSecondController.GetLastAnalogReadTime() > lastTimeZWasApplied)
                        {
                            double laserRead = App.Current.URSecondController.GetAnalogValue();
                            double laserWithLimit = UniversalRobotHelperFunctions.LimitAnalogRead(laserRead);
                            lastTimeZWasApplied = DateTime.Now;
                            if (hasInitLaserAverage)
                            {
                                AddNewReadToPool(laserWithLimit);
                            }
                            else
                            {
                                InitializeLaserAveragePool(laserWithLimit);
                            }

                        }
                        if (hasInitLaserAverage && hasRobotReachedPosition)
                        {
                            hasStartedTrackingLazer = true;

                            Vector3D lookDirection = new Vector3D(
                                universalRobotCameraSetpoint.qx,
                                universalRobotCameraSetpoint.qy,
                                universalRobotCameraSetpoint.qz
                                );

                            Point3D currentCamSetPoint = new Point3D(
                                setpoint.X,
                                setpoint.Y,
                                setpoint.Z);

                            //if the camera coord hasnt moved we can try using the robots current coord to get a more accurate track
                            currentCamSetPoint = new Point3D(
                            currentRobotCoord.x,
                            currentRobotCoord.y,
                            currentRobotCoord.z);

                            double analogConverted = UniversalRobotHelperFunctions.ConvertAnalogReaderToMM(
                                GetLaserRead()
                            );

                            if ((DateTime.Now - lastTimeZWasMoved) > TimeSpan.FromSeconds(0.5)
                                && !double.IsNaN(analogConverted))
                            {
                                Point3D translatedWithZ = UniversalRobotHelperFunctions.AdjustLocationToLaser(
                                    currentCamSetPoint,
                                    lookDirection,
                                    analogConverted,
                                    initialLaserReadMM,
                                    0.7 //bias the movement
                                    );

                                setpoint = translatedWithZ;
                                lastTimeZWasMoved = DateTime.Now;
                            }
                            else
                            {
                                shouldUpdateMoveSetpoint = false;
                            }
                        }
                        //End laser tracking                    
                    }

                    //check the force sensor again before checking range
                    CheckForceSensor();

                    CheckIfPointIsOutOfRange(setpoint);

                    if (shouldUpdateMoveSetpoint)
                    {
                        urController.UpdateRobotCoordinate(
                            setpoint.X,
                            setpoint.Y,
                            setpoint.Z,
                            universalRobotCameraSetpoint.qx,
                            universalRobotCameraSetpoint.qy,
                            universalRobotCameraSetpoint.qz);

                        Console.WriteLine("Setpoint x:" + setpoint.X.ToString() +
                            ", y:" + setpoint.Y.ToString() +
                            ", z:" + setpoint.Z.ToString());

                        //Update the current setpoint for the ui
                        CurrentSetPoint.x = setpoint.X;
                        CurrentSetPoint.y = setpoint.Y;
                        CurrentSetPoint.z = setpoint.Z;
                        CurrentSetPoint.qx = universalRobotCameraSetpoint.qx;
                        CurrentSetPoint.qy = universalRobotCameraSetpoint.qy;
                        CurrentSetPoint.qz = universalRobotCameraSetpoint.qz;
                    }


                    stop = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                ErrorHasOccurred = true;
                LastErrorMessage = ex.Message;
                UpdateErrorStatus(new EventArgs());
            }
        }

        public event EventHandler InitOccured;
        public virtual void UpdateInitStatus(EventArgs e)
        {
            if (InitOccured != null)
            {
                InitOccured(this, e);
            }
        }

        public event EventHandler ErrorOccured;
        public virtual void UpdateErrorStatus(EventArgs e)
        {
            if (ErrorOccured != null)
            {
                ErrorOccured(this, e);
            }
        }
    }
}
