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
        public double AccelerationSpeed;
        UniversalRobotController.URRobotCoOrdinate lastCycleDeltas = new UniversalRobotController.URRobotCoOrdinate();

        //RigidBodyDeltas lastCycleDeltas = new RigidBodyDeltas();
        //Creating lasy cycle rpy delta


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

        struct RPY_RigidBodyDeltas
        {
            public double rDelta;
            public double pDelta;
            public double yDelta;
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
            TorqueSensorTracking torqueSensor,
            double accelerationDefault
           )
        {
            urController = universalRobotController;
            polarisController = polarisCameraController;
            coordTrans = cameraTranslator;
            appSettings = applicationSettings;
            urController2 = secondaryController;
            torqueSensorTracker = torqueSensor;
            AccelerationSpeed = accelerationDefault;
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


        // Implemented RPY
        UniversalRobotController.URRobotCoOrdinate ConvertRigidBodyIntoURSpace(UniversalRobotController.URRobotCoOrdinate polaris)
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
            converted.xDelta = (polaris.z / 1000);
            converted.yDelta = (polaris.y / 1000);
            converted.zDelta = (polaris.x / 1000);// ((-polaris.xDelta) / 1000);

            RPY_RigidBodyDeltas rpy_converted = new RPY_RigidBodyDeltas();
            rpy_converted.rDelta = (polaris.qz / 1000);
            rpy_converted.pDelta = (polaris.qy / 1000);
            rpy_converted.yDelta = (polaris.qz / 1000);

            UniversalRobotController.URRobotCoOrdinate convertedDelta = new UniversalRobotController.URRobotCoOrdinate();

            convertedDelta.x = converted.xDelta;
            convertedDelta.y = converted.yDelta;
            convertedDelta.z = converted.zDelta;
            convertedDelta.qx = rpy_converted.rDelta;
            convertedDelta.qy = rpy_converted.pDelta;
            convertedDelta.qz = rpy_converted.yDelta;
            return convertedDelta;
        }

        UniversalRobotController.URRobotCoOrdinate GetRigidBodyDeltas()
        {
            RigidBodyDeltas delta = new RigidBodyDeltas();
            RPY_RigidBodyDeltas RPYdelta = new RPY_RigidBodyDeltas();

            Vector3D rpycurrentlocations = new Vector3D();
            Vector3D rpyrigidbodysetpoints = new Vector3D();

            UniversalRobotController.URRobotCoOrdinate convertedDelta = new UniversalRobotController.URRobotCoOrdinate();

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


            //Converting current location and rigibody set points from quartenion to rpy

            rpycurrentlocations = coordTrans.QuaternionToRPY(currentLocations.qo, currentLocations.qx, currentLocations.qy, currentLocations.qz);
            rpyrigidbodysetpoints = coordTrans.QuaternionToRPY(rigidBodySetpoints.qo, rigidBodySetpoints.qx, rigidBodySetpoints.qy, rigidBodySetpoints.qz);

            //Calculating RPYdelta
            RPYdelta.rDelta = rpycurrentlocations.X - rpyrigidbodysetpoints.X;
            RPYdelta.pDelta = rpycurrentlocations.Y - rpyrigidbodysetpoints.Y;
            RPYdelta.yDelta = rpycurrentlocations.Z - rpyrigidbodysetpoints.Z;

            convertedDelta.x = delta.xDelta;
            convertedDelta.y = delta.yDelta;
            convertedDelta.z = delta.zDelta;
            convertedDelta.qx = RPYdelta.rDelta;
            convertedDelta.qy = RPYdelta.pDelta;
            convertedDelta.qz = RPYdelta.yDelta;


            return ConvertRigidBodyIntoURSpace(convertedDelta);
        }

        UniversalRobotController.URRobotCoOrdinate GetCameraRigidBody()
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

        // TODO: Implement for qx, qy and qz after defining setPoint properly
        bool HasRobotReachedCameraSetpoint(UniversalRobotController.URRobotCoOrdinate currentRobotCoord, Point3D setPoint, Point3D rpy_setPoint)
        {
            double tolerance = 0.0002;
            if (Math.Abs(Math.Abs(currentRobotCoord.x) - Math.Abs(setPoint.X)) < tolerance &&
                Math.Abs(Math.Abs(currentRobotCoord.y) - Math.Abs(setPoint.Y)) < tolerance &&
                Math.Abs(Math.Abs(currentRobotCoord.z) - Math.Abs(setPoint.Z)) < tolerance &&
                Math.Abs(Math.Abs(currentRobotCoord.qx) - Math.Abs(rpy_setPoint.X)) < tolerance &&
                Math.Abs(Math.Abs(currentRobotCoord.qy) - Math.Abs(rpy_setPoint.Y)) < tolerance &&
                Math.Abs(Math.Abs(currentRobotCoord.qz) - Math.Abs(rpy_setPoint.Z)) < tolerance)
            {
                return true;
            }
            return false;
        }

        //Implemented RPY to check the tolerance (TODO)

        bool IsWithinRangeForLazerTracking(UniversalRobotController.URRobotCoOrdinate lastDeltas, UniversalRobotController.URRobotCoOrdinate currentDeltas)
        {
            double tolerance = 0.0002;
            if (Math.Abs(Math.Abs(currentDeltas.x) - Math.Abs(lastDeltas.x)) < tolerance &&
                Math.Abs(Math.Abs(currentDeltas.y) - Math.Abs(lastDeltas.y)) < tolerance &&
                Math.Abs(Math.Abs(currentDeltas.z) - Math.Abs(lastDeltas.z)) < tolerance &&
                Math.Abs(Math.Abs(currentDeltas.qx) - Math.Abs(lastDeltas.qx)) < tolerance &&
                Math.Abs(Math.Abs(currentDeltas.qy) - Math.Abs(lastDeltas.qy)) < tolerance &&
                Math.Abs(Math.Abs(currentDeltas.qz) - Math.Abs(lastDeltas.qz)) < tolerance)
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

        //TODO (Implemented rounding operation for rpy deltas)

        (Point3D,Point3D) GetSetpointFromDeltas(UniversalRobotController.URRobotCoOrdinate convertedDelta)
        {
            //ignore z moves
            //deltas.zDelta = 0;

            convertedDelta.x = Math.Round(convertedDelta.x, 5);
            convertedDelta.y = Math.Round(convertedDelta.y, 5);
            convertedDelta.z = Math.Round(convertedDelta.z, 5);
            convertedDelta.qx = Math.Round(convertedDelta.qx, 5);
            convertedDelta.qy = Math.Round(convertedDelta.qy, 5);
            convertedDelta.qz = Math.Round(convertedDelta.qz, 5);




            Point3D deltaPoint = new Point3D(convertedDelta.x, convertedDelta.y, convertedDelta.z);
            Point3D deltaWithRotation = coordTrans.GetPointWithBaseRoation(deltaPoint);

            Point3D rpy_deltaPoint = new Point3D(convertedDelta.qx, convertedDelta.qy, convertedDelta.qz);
            Point3D rpy_deltaWithRotation = coordTrans.GetPointWithBaseRoation(rpy_deltaPoint);

            //then we can apply the deltas that were returned
            Point3D setpoint = new Point3D(
                universalRobotCameraSetpoint.x + deltaWithRotation.X,
                universalRobotCameraSetpoint.y + deltaWithRotation.Y,
                universalRobotCameraSetpoint.z + deltaWithRotation.Z
                );

            Point3D rpy_setpoint = new Point3D(
                universalRobotCameraSetpoint.qx + rpy_deltaWithRotation.X,
                universalRobotCameraSetpoint.qy + rpy_deltaWithRotation.Y,
                universalRobotCameraSetpoint.qz + rpy_deltaWithRotation.Z
                );

            return (setpoint, rpy_setpoint);
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
                    //TODO: Define rpy_deltas properly
                    //RigidBodyDeltas deltas, RPY_RigidBodyDeltas  rpy_deltas = GetCameraRigidBody();
                    //RPY_RigidBodyDeltas rpy_deltas = GetCameraRigidBody();

                    UniversalRobotController.URRobotCoOrdinate convertedDelta = GetCameraRigidBody();



                    //Get the setpoint from the camera
                    //TODO: (Implemented rpy_setpoint)
                    //Check
                    Point3D setpoint = new Point3D();
                    Point3D rpy_setpoint = new Point3D();
                    (setpoint, rpy_setpoint) = GetSetpointFromDeltas(convertedDelta);



                    //end camera tracking
                    UniversalRobotController.URRobotCoOrdinate currentRobotCoord = urController.GetCurrentLocation();

                    //make sure that the robot has reached its position

                    //TODO
                    bool isWithingRangeForZMoves = IsWithinRangeForLazerTracking(lastCycleDeltas, convertedDelta);
                    if (!isWithingRangeForZMoves)
                    {
                        hasRobotReachedPosition = false;
                        if (hasStartedTrackingLazer)
                        {
                            ResetDeltaFlag();
                            //this will find a marker and set the current xyz of robot
                            convertedDelta = GetCameraRigidBody();
                            //Get the setpoint from the camera
                            (setpoint, rpy_setpoint) = GetSetpointFromDeltas(convertedDelta);
                            hasStartedTrackingLazer = false;
                        }

                        //lastCycleDeltas = deltas;
                    }
                    else
                    {
                        //only start doing z tracking once the position has been met by the robot                        
                        bool hasRobotReachedCameraCoOrd = HasRobotReachedCameraSetpoint(currentRobotCoord, setpoint, rpy_setpoint);
                        if (hasRobotReachedPosition ||
                            hasRobotReachedCameraCoOrd)
                        {
                            //we have reached position lock in the bool until the camera markers move again
                            hasRobotReachedPosition = true;
                        }
                    }
                    lastCycleDeltas = convertedDelta;

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


                    //TODO
                    if (shouldUpdateMoveSetpoint)
                    {
                        urController.UpdateRobotCoordinate(
                            setpoint.X,
                            setpoint.Y,
                            setpoint.Z,
                            rpy_setpoint.X,
                            rpy_setpoint.Y,
                            rpy_setpoint.Z,
                            true,
                            AccelerationSpeed);

                        Console.WriteLine("Setpoint x:" + setpoint.X.ToString() +
                            ", y:" + setpoint.Y.ToString() +
                            ", z:" + setpoint.Z.ToString());

                        //Update the current setpoint for the ui
                        //TODO
                        CurrentSetPoint.x = setpoint.X;
                        CurrentSetPoint.y = setpoint.Y;
                        CurrentSetPoint.z = setpoint.Z;
                        CurrentSetPoint.qx = rpy_setpoint.X;
                        CurrentSetPoint.qy = rpy_setpoint.Y;
                        CurrentSetPoint.qz = rpy_setpoint.Z;
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
