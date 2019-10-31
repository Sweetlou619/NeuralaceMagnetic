using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections;

namespace NeuralaceMagnetic.Controls
{
    public class UniversalRobotController : BackgroundWorker, INotifyPropertyChanged
    {
        public struct URRobotCoOrdinate
        {
            public DateTime setTime;
            public double x;
            public double y;
            public double z;
            public double qx;
            public double qy;
            public double qz;

            public override String ToString()
            {
                return String.Format("{0:N2}, {1:N2}, {2:N2}, {3:N2}, {4:N2}, {5:N2}", x, y, z, qx, qy, qz);
            }

            public static URRobotCoOrdinate operator -(URRobotCoOrdinate r1, URRobotCoOrdinate r2)
            {
                URRobotCoOrdinate newVal = new URRobotCoOrdinate();
                newVal.x = r1.x - r2.x;
                newVal.y = r1.y - r2.y;
                newVal.z = r1.z - r2.z;
                newVal.qx = r1.qx - r2.qx;
                newVal.qy = r1.qy - r2.qy;
                newVal.qz = r1.qz - r2.qz;

                return newVal;
            }
        }

        private bool hasReachedPosition = true;
        public bool HasReachedPosition
        {
            get
            {
                return hasReachedPosition;
            }

            set
            {
                if (value != hasReachedPosition)
                {
                    hasReachedPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime lastMoveTime = DateTime.MinValue;
        private const double DefaultMaximumMoveDistanceM = 0.040; //40mm
        private double MaximumMoveDistanceM = DefaultMaximumMoveDistanceM; //40mm
        private const double DefaultMoveCommandTimeMS = 350; //per 250ms
        private double MoveCommandTimeMS = DefaultMoveCommandTimeMS; //per 250ms
        private UniversalRobotNoPendantController universalRobotNoPendantController
        {
            get
            {
                return App.Current.URNoPendantControl;
            }
        }

        public bool IsUniversalRobotConnected = false;
        public UniversalRobotRealTimeTCPStatus URRobotStatus;
        public int commErrors = 0;

        private Queue<string> m_CommandQueue = new Queue<string>();

        private TcpClient m_URTcpClient;
        
        URRobotCoOrdinate moveCoOrdinate;
        bool shouldUseAnglesInMove = false;

        bool fireDigitalOutputWhenPositionIsReached = false;
        DateTime digitalOutputSetTime = DateTime.MinValue;
        bool digitalOutputIsHigh = false;

        bool isVirtualEStopped = false;

        private bool virtualEstopOverride = false;
        private bool isVirtualEStoppedOverriden
        {
            get
            {
                return virtualEstopOverride;
            }
            set 
            {
                virtualEstopOverride = value;
            }
        }


        bool isVirtualEStopMoveRunning = false;
        bool shouldJogAxis = false;
        bool jogPositive = true;
        public enum eJogAxis
        {
            X, Y, Z
        };
        eJogAxis axisToJog = eJogAxis.X;

        public UniversalRobotController()
        {
            IsUniversalRobotConnected = false;

            //never move to the move coordinate unless it has been set by the caller
            moveCoOrdinate.setTime = DateTime.MinValue;
        }

        public bool GetFreeDriveStatus()
        {
            return universalRobotNoPendantController.IsFreeDriveEnabled();
        }

        void ConnectToUniversalRobotTCPServer()
        {
            try
            {
                m_URTcpClient = new TcpClient();
                m_URTcpClient.ReceiveTimeout = 1000;
                m_URTcpClient.SendTimeout = 1000;
                //home ur5
                m_URTcpClient.Connect(App.Current.ApplicationSettings.URIpAddress, 30003);
                //shiv ur5
                //m_URTcpClient.Connect("192.168.1.2", 30003);               
                IsUniversalRobotConnected = true;
            }
            catch (Exception e)
            {
                IsUniversalRobotConnected = false;
                //give the other threads some time to context switch
                Thread.Sleep(1000);
            }
        }

        public void UseTrackingMotionSettings()
        {
            MaximumMoveDistanceM = App.Current.ApplicationSettings.MaxTrackingMovePerWindowMM / 1000;
            MoveCommandTimeMS = App.Current.ApplicationSettings.MaxTrackingTimeWindowMS;
        }

        public void DisableTrackingMotionSettings()
        {
            MaximumMoveDistanceM = DefaultMaximumMoveDistanceM;
            MoveCommandTimeMS = DefaultMoveCommandTimeMS;
        }

        public void StopRobotMove()
        {
            fireDigitalOutputWhenPositionIsReached = false;
            HasReachedPosition = true;
        }

        public void SetFreeDriveMode(bool freeDrive)
        {
            if (freeDrive)
            {
                universalRobotNoPendantController.TurnOnFreeDrive();
            }
            else
            {
                universalRobotNoPendantController.TurnOffFreeDrive();
            }
        }

        public void SetVirtualEStopOverride(bool estopValue)
        {
            isVirtualEStoppedOverriden = estopValue;
        }

        public void SetVirtualEStopOverride(bool estopValue, double x, double y, double z)
        {
            moveCoOrdinate.setTime = DateTime.Now;
            moveCoOrdinate.x = x;
            moveCoOrdinate.y = y;
            moveCoOrdinate.z = z;
            isVirtualEStopMoveRunning = true;
        }

        public void FireDigitalOutputWhenPositionIsReached()
        {
            fireDigitalOutputWhenPositionIsReached = true;
        }

        public void FireDigitalOutput()
        {
            //send the on command now
            SendCommandToRobot("set_digital_out(1, True)\n");
            digitalOutputSetTime = DateTime.Now;
            digitalOutputIsHigh = true;
        }

        public void ResetDigitalOutput()
        {
            SendCommandToRobot("set_digital_out(1, False)\n");
            digitalOutputIsHigh = false;
        }

        public URRobotCoOrdinate GetCurrentLocation()
        {
            URRobotCoOrdinate coord = new URRobotCoOrdinate();
            coord.x = URRobotStatus.ToolVectorActual_1;
            coord.y = URRobotStatus.ToolVectorActual_2;
            coord.z = URRobotStatus.ToolVectorActual_3;
            coord.qx = URRobotStatus.ToolVectorActual_4;
            coord.qy = URRobotStatus.ToolVectorActual_5;
            coord.qz = URRobotStatus.ToolVectorActual_6;
            return coord;
        }

        public void MoveToHomePostition()
        {
            m_CommandQueue.Enqueue(
                "movej([1.5708,-1.5708,1.5708,-1.5708,-1.5708,1.5708],a=1.0, v=0.1)\n"
                );
        }

        public void MoveToSavedPosition(double x, double y, double z, double rx, double ry, double rz)
        {
            m_CommandQueue.Enqueue(
                "movej(p["+x+","+y+","+z+","+rx+","+ry+","+rz+"],a=1.0, v=0.1)\n"
                );
        }

        private void FlipEndian(Type type, byte[] data)
        {
            var fields = type.GetFields().Where(f => f.IsPublic)
                .Select(f => new
                {
                    Field = f,
                    Offset = Marshal.OffsetOf(type, f.Name).ToInt32()
                }).ToList();

            foreach (var field in fields)
            {
                Array.Reverse(data, field.Offset, Marshal.SizeOf(field.Field.FieldType));
            }
        }

        public void UpdateRobotCoordinate(double x, double y, double z, double qx, double qy, double qz, bool manualOverrideAngles = true, double accelerationSpeed = DefaultMoveCommandTimeMS)
        {
            if (isVirtualEStopMoveRunning)
                return;

            shouldUseAnglesInMove = true;//manualOverrideAngles;
            moveCoOrdinate.setTime = DateTime.Now;
            moveCoOrdinate.x = x;
            moveCoOrdinate.y = y;
            moveCoOrdinate.z = z;
            moveCoOrdinate.qx = qx;
            moveCoOrdinate.qy = qy;
            moveCoOrdinate.qz = qz;
            MoveCommandTimeMS = accelerationSpeed;
        }

        bool IsGoodStatusPacket(UniversalRobotRealTimeTCPStatus status)
        {
            double outOfRange = 5;
            double outOfRangeAngle = 100;
            if (Math.Abs(status.ToolVectorActual_1) > outOfRange ||
                Math.Abs(status.ToolVectorActual_2) > outOfRange ||
                Math.Abs(status.ToolVectorActual_3) > outOfRange ||
                Math.Abs(status.ToolVectorActual_4) > outOfRangeAngle ||
                Math.Abs(status.ToolVectorActual_5) > outOfRangeAngle ||
                Math.Abs(status.ToolVectorActual_6) > outOfRangeAngle
                )
            {
                return false;
            }
            return true;
        }

        private bool IsRobotAbleToPerformMove()
        {
            //only move if the virtual estop is not pressed and the robot is in the run mode
            bool ableToMove = !IsVirtualEStopPressed() && URRobotStatus.RobotMode == 7;
            return ableToMove;
        }

        private const int UNIVERSAL_ROBOT_TCP_STATUS_BUFFER_SIZE = 1116;

        void UpdateUniversalRobotStatus()
        {
            Stream stream = m_URTcpClient.GetStream();
            //UR robot v5.4 returns a 1116 byte packet
            //Grab the last 10 packets and parse the newest one
            byte[] allBuffer = new byte[(10 * UNIVERSAL_ROBOT_TCP_STATUS_BUFFER_SIZE)];
            int k = stream.Read(allBuffer, 0, (10 * UNIVERSAL_ROBOT_TCP_STATUS_BUFFER_SIZE));
            int startIndex = k - UNIVERSAL_ROBOT_TCP_STATUS_BUFFER_SIZE;
            byte[] bb = new byte[UNIVERSAL_ROBOT_TCP_STATUS_BUFFER_SIZE];
            Array.Copy(allBuffer, startIndex, bb, 0, UNIVERSAL_ROBOT_TCP_STATUS_BUFFER_SIZE);

            FlipEndian(typeof(UniversalRobotRealTimeTCPStatus), bb);
            GCHandle handle = GCHandle.Alloc(bb, GCHandleType.Pinned);
            UniversalRobotRealTimeTCPStatus robotStatus = (UniversalRobotRealTimeTCPStatus)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(UniversalRobotRealTimeTCPStatus));
            if (IsGoodStatusPacket(robotStatus))
            {
                if (robotStatus.QD_Actual_1 != URRobotStatus.QD_Actual_1 && robotStatus.QD_Actual_1 == (double)0)
                {
                    OnPropertyChanged("ProgramState");
                }

                URRobotStatus = robotStatus;
            }
            handle.Free();
        }

        bool CheckMoveCoordinate(double destination, double current, ref double output)
        {
            double difference = destination - current;
            if (Math.Abs(difference) > MaximumMoveDistanceM)
            {
                if (difference > 0)
                {
                    output = current + MaximumMoveDistanceM;
                }
                else
                {
                    output = current - MaximumMoveDistanceM;
                }
                return true;
            }

            output = destination;
            return false;
        }

        void SendMoveCommands()
        {
            if (isVirtualEStopMoveRunning
                &&
                lastMoveTime > moveCoOrdinate.setTime
                &&
                HasReachedPosition)
            {
                isVirtualEStopMoveRunning = false;
                isVirtualEStoppedOverriden = true;
                Thread.Sleep(100);
            }

            //If the robot is not in a running state ignore all move commands
            if (!IsRobotAbleToPerformMove())
            {
                HasReachedPosition = true;
                lastMoveTime = DateTime.Now;
                return;
            }

            if (moveCoOrdinate.setTime > lastMoveTime
                || !HasReachedPosition)
            {
                double moveX = moveCoOrdinate.x;
                double moveY = moveCoOrdinate.y;
                double moveZ = moveCoOrdinate.z;
                double moveqX = moveCoOrdinate.qx;
                double moveqY = moveCoOrdinate.qy;
                double moveqZ = moveCoOrdinate.qz;

                //limit the move to the specifeid limit value
                bool hasXBeenLimited = CheckMoveCoordinate(moveCoOrdinate.x, URRobotStatus.ToolVectorActual_1, ref moveX);
                bool hasYBeenLimited = CheckMoveCoordinate(moveCoOrdinate.y, URRobotStatus.ToolVectorActual_2, ref moveY);
                bool hasZBeenLimited = CheckMoveCoordinate(moveCoOrdinate.z, URRobotStatus.ToolVectorActual_3, ref moveZ);
                //bool hasQXBeenLimited = CheckMoveCoordinate(moveCoOrdinate.qx, URRobotStatus.ToolVectorActual_4, ref moveqX);
                //bool hasQYBeenLimited = CheckMoveCoordinate(moveCoOrdinate.qy, URRobotStatus.ToolVectorActual_5, ref moveqY);
                //bool hasQZBeenLimited = CheckMoveCoordinate(moveCoOrdinate.qz, URRobotStatus.ToolVectorActual_6, ref moveqZ);

                if ((hasXBeenLimited || hasYBeenLimited || hasZBeenLimited) && !shouldUseAnglesInMove)
                //||
                //hasQXBeenLimited ||
                //hasQYBeenLimited ||
                //hasQZBeenLimited)
                {
                    //if a move has been limited we will need to send another command
                    HasReachedPosition = false;
                }
                else
                {
                    HasReachedPosition = true;
                }

                string moveTime = (MoveCommandTimeMS / 1000).ToString();


                string command = "";
                if (shouldUseAnglesInMove)
                {
                    command = "servoj(get_inverse_kin(p["
                    + moveCoOrdinate.x.ToString() + ", "
                    + moveCoOrdinate.y.ToString() + ", "
                    + moveCoOrdinate.z.ToString() + ", ";
                    command += moveCoOrdinate.qx.ToString() + ", "
                            + moveCoOrdinate.qy.ToString() + ", "
                            + moveCoOrdinate.qz.ToString() + "]), ";
                   // command += "a=2.0, v=0.1)";
                    command += "t=" + moveTime + ", lookahead_time=0.03)";

                }
                else
                {
                    command = "servoj(get_inverse_kin(p["
                    + moveX.ToString() + ", "
                    + moveY.ToString() + ", "
                    + moveZ.ToString() + ", ";
                    command += URRobotStatus.ToolVectorActual_4.ToString() + ", "
                            + URRobotStatus.ToolVectorActual_5.ToString() + ", "
                            + URRobotStatus.ToolVectorActual_6.ToString() + "]), ";
                    command += "t=" + moveTime + ", lookahead_time=0.03)"; //move over the time specified
                }  
                command += "\n";

                SendCommandToRobot(command);
                //mark the latest send time
                lastMoveTime = DateTime.Now;
            }

            if (shouldJogAxis)
            {
                SendCommandToRobot(GetJogCommand());
                shouldJogAxis = false;
            }
        }

        public void JogAxis(eJogAxis axis, bool positive = true)
        {
            axisToJog = axis;
            jogPositive = positive;
            shouldJogAxis = true;
        }

        string GetJogCommand()
        {
            double x = 0;
            double y = 0;
            double z = 0;
            double jogSpeed = 0.05;
            if (!jogPositive)
            {
                jogSpeed = -jogSpeed;
            }

            if (axisToJog == eJogAxis.X)
            {
                x = jogSpeed;
            }
            else if (axisToJog == eJogAxis.Y)
            {
                y = jogSpeed;
            }
            else if (axisToJog == eJogAxis.Z)
            {
                z = jogSpeed;
            }

            return "speedl([0,0,0," + x.ToString() + "," + y.ToString() + "," + z.ToString() + "], "
                   + "a=0.5,"
                   + "t=0.5)" //move over the time specified                 
                   + "\n";
        }

        void CheckCommandQueue()
        {
            if (fireDigitalOutputWhenPositionIsReached
                && (moveCoOrdinate.setTime <= lastMoveTime
                && HasReachedPosition
                && (DateTime.Now - lastMoveTime).TotalMilliseconds > MoveCommandTimeMS + 100))
            {
                FireDigitalOutput();
                fireDigitalOutputWhenPositionIsReached = false;
            }
            else
            {
                if (digitalOutputIsHigh
                    && ((DateTime.Now - digitalOutputSetTime).TotalMilliseconds > 100))
                {
                    ResetDigitalOutput();
                }
                else
                {
                    if (m_CommandQueue.Count > 0)
                    {
                        SendCommandToRobot(m_CommandQueue.Dequeue());
                    }
                }
            }
        }

        void SendCommandToRobot(string command)
        {
            Stream stream = m_URTcpClient.GetStream();
            Encoding enc = new UnicodeEncoding(true, true, true);
            byte[] arrayBytesAnswer = ASCIIEncoding.ASCII.GetBytes(command);
            stream.Write(arrayBytesAnswer, 0, arrayBytesAnswer.Length);
        }

        public void Start()
        {
            this.RunWorkerAsync();
        }

        public void Stop()
        {
            if (this.IsBusy == true)
            {
                this.CancelAsync();
            }
        }

        public bool IsVirtualEStopPressed()
        {
            try
            {
                Int64 bits = Convert.ToInt64(URRobotStatus.DigitalInputBits);
                Int64 checkBit = 0;
                //TODO (9/4/2019): revisit this check when we have estop
                //return ((bits & checkBit) == checkBit) || isVirtualEStoppedOverriden;
            }
            catch { }
            return false;
        }

        public bool IsToolFreedrivePressed()
        {
            try
            {
                Int64 bits = Convert.ToInt64(URRobotStatus.DigitalInputBits);
                Int64 checkBit = 65536;
                bool isFreedrivePressed = ((bits & checkBit) == checkBit);
                return !isFreedrivePressed;
            }
            catch { }
            return false;
        }

        public bool IsFreeDriveMode()
        {
            return universalRobotNoPendantController.IsFreeDriveEnabled();
        }

        bool freedriveLastState = false;
        
        void CheckFreedriveToolButton()
        {
            bool currentState = IsToolFreedrivePressed();
            if (currentState != freedriveLastState && currentState)
            {
                SetVirtualEStopOverride(!isVirtualEStoppedOverriden);
            }
            freedriveLastState = currentState;
        }

        void CheckVirtualEStop()
        {
            CheckFreedriveToolButton();

            if (IsVirtualEStopPressed())
            {
                StopRobotMove();
                if (!IsFreeDriveMode())
                {
                    //SetFreeDriveMode(true);
                }
                isVirtualEStopped = true;
            }
            else
            {
                if (isVirtualEStopped)
                {
                    //SetFreeDriveMode(false);
                    isVirtualEStopped = false;
                }
            }
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            DateTime start = new DateTime();
            DateTime stop = new DateTime();
            TimeSpan span = new TimeSpan();
            while (true)
            {
                //span = stop - start;
                //span = TimeSpan.FromMilliseconds(1) - span;
                //if (span <= TimeSpan.FromMilliseconds(0))
                //{
                //    Thread.Sleep(TimeSpan.FromMilliseconds(1));
                //}
                //else
                //{
                //    Thread.Sleep(span);
                //}
                start = DateTime.Now;

                if (!IsUniversalRobotConnected)
                {
                    ConnectToUniversalRobotTCPServer();
                }
                else
                {
                    try
                    {
                        UpdateUniversalRobotStatus();
                        CheckVirtualEStop();
                        CheckCommandQueue();
                        //only send move commands after the specified time
                        //tolerance value. there seems to be lag between when the move is send and when the move is excuted
                        //in milliseconds adjust this value to get a faster response time between consecutive moves
                        double toleranceTime = 50;
                        if ((DateTime.Now - lastMoveTime).TotalMilliseconds > (MoveCommandTimeMS - toleranceTime))
                        {
                            SendMoveCommands();
                        }
                    }
                    catch (Exception ex)
                    {
                        IsUniversalRobotConnected = false;
                        commErrors++;
                    }
                }
                stop = DateTime.Now;
            }
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}
