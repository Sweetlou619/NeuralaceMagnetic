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
    public class TorqueSensorTracking : BackgroundWorker
    {
        bool dotracking;
        bool trackForceSensor = false;
        bool errorStateActive = false;
        
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

        public void SetForceTracking(bool enable)
        {
            trackForceSensor = enable;
            
            if (!trackForceSensor)
                ResetErrorState();
        }

        public void ResetErrorState()
        {
            errorStateActive = false;
        }

        public bool IsErrorActive()
        {
            return errorStateActive;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            DateTime start = new DateTime();
            DateTime stop = new DateTime();
            while (dotracking)
            {
                start = DateTime.Now;

                if (trackForceSensor)
                {
                    if (!errorStateActive && UniversalRobotHelperFunctions.IsForceOverLimit())
                    {
                        errorStateActive = true;
                        Vector3D forceDirection = UniversalRobotHelperFunctions.GetForceDirection();
                        UniversalRobotController.URRobotCoOrdinate currentRobotCoord = App.Current.URController.GetCurrentLocation();
                        Point3D currentCamSetPoint = new Point3D(
                                        currentRobotCoord.x,
                                        currentRobotCoord.y,
                                        currentRobotCoord.z);
                        Vector3D lookDirection = new Vector3D(
                                        currentRobotCoord.qx,
                                        currentRobotCoord.qy,
                                        currentRobotCoord.qz
                                        );

                        Point3D safePoint = UniversalRobotHelperFunctions.AdjustLocationToForceSensor(currentCamSetPoint, lookDirection, forceDirection);
                        App.Current.URController.SetVirtualEStopOverride(true, safePoint.X, safePoint.Y, safePoint.Z);
                    }
                }

                stop = DateTime.Now;
                Thread.Sleep(20);
            }
        }
    }
}
