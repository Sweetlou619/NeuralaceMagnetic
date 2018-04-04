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
using System.Collections;

namespace NeuralaceMagnetic.Controls
{
    public class UniversalRobotController : BackgroundWorker
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
        }

        private DateTime lastMoveTime = DateTime.MinValue;
        private const double DefaultMaximumMoveDistanceM = 0.040; //40mm
        private double MaximumMoveDistanceM = DefaultMaximumMoveDistanceM; //40mm
        private const double DefaultMoveCommandTimeMS = 350; //per 250ms
        private double MoveCommandTimeMS = DefaultMoveCommandTimeMS; //per 250ms

        public bool IsUniversalRobotConnected = false;
        public UniversalRobotRealTimeTCPStatus URRobotStatus;
        public int commErrors = 0;

        private Queue<string> m_CommandQueue = new Queue<string>();

        private TcpClient m_URTcpClient;

        bool hasReachedPosition = true;
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
            return isVirtualEStopped;
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

        public bool HasRobotReachedPosition()
        {
            return hasReachedPosition;
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
            hasReachedPosition = true;
        }

        public void SetFreeDriveMode(bool freeDrive)
        {
            if (freeDrive)
            {
                m_CommandQueue.Enqueue("set_digital_out(0, True)\n");
            }
            else
            {
                m_CommandQueue.Enqueue("set_digital_out(0, False)\n");
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

        public void UpdateRobotCoordinate(double x, double y, double z, double qx, double qy, double qz, bool manualOverrideAngles = false)
        {
            if (isVirtualEStopMoveRunning)
                return;

            shouldUseAnglesInMove = manualOverrideAngles;
            moveCoOrdinate.setTime = DateTime.Now;
            moveCoOrdinate.x = x;
            moveCoOrdinate.y = y;
            moveCoOrdinate.z = z;
            moveCoOrdinate.qx = qx;
            moveCoOrdinate.qy = qy;
            moveCoOrdinate.qz = qz;
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

        void UpdateUniversalRobotStatus()
        {
            Stream stream = m_URTcpClient.GetStream();
            //UR robot v3.4 returns a 1060 byte packet
            //Grab the last 10 packets and parse the newest one
            byte[] allBuffer = new byte[(10 * 1060)];
            int k = stream.Read(allBuffer, 0, (10 * 1060));
            int startIndex = k - 1060;
            byte[] bb = new byte[1060];
            Array.Copy(allBuffer, startIndex, bb, 0, 1060);

            FlipEndian(typeof(UniversalRobotRealTimeTCPStatus), bb);
            GCHandle handle = GCHandle.Alloc(bb, GCHandleType.Pinned);
            UniversalRobotRealTimeTCPStatus robotStatus = (UniversalRobotRealTimeTCPStatus)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(UniversalRobotRealTimeTCPStatus));
            if (IsGoodStatusPacket(robotStatus))
            {
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
                hasReachedPosition)
            {
                isVirtualEStopMoveRunning = false;
                isVirtualEStoppedOverriden = true;
                Thread.Sleep(100);
            }

            //If the robot is not in a running state ignore all move commands
            if (!IsRobotAbleToPerformMove())
            {
                hasReachedPosition = true;
                lastMoveTime = DateTime.Now;
                return;
            }

            if (moveCoOrdinate.setTime > lastMoveTime
                || !hasReachedPosition)
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
                    hasReachedPosition = false;
                }
                else
                {
                    hasReachedPosition = true;
                }

                string moveTime = (MoveCommandTimeMS / 1000).ToString();


                string command = "";
                if (shouldUseAnglesInMove)
                {
                    command = "movej(p["
                    + moveCoOrdinate.x.ToString() + ", "
                    + moveCoOrdinate.y.ToString() + ", "
                    + moveCoOrdinate.z.ToString() + ", ";
                    command += moveCoOrdinate.qx.ToString() + ", "
                            + moveCoOrdinate.qy.ToString() + ", "
                            + moveCoOrdinate.qz.ToString() + "], ";
                    command += "t=" + 3 + ")";
                }
                else
                {
                    command = "movej(p["
                    + moveX.ToString() + ", "
                    + moveY.ToString() + ", "
                    + moveZ.ToString() + ", ";
                    command += URRobotStatus.ToolVectorActual_4.ToString() + ", "
                            + URRobotStatus.ToolVectorActual_5.ToString() + ", "
                            + URRobotStatus.ToolVectorActual_6.ToString() + "], ";
                    command += "t=" + moveTime + ")"; //move over the time specified
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
                && hasReachedPosition
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
                Int64 checkBit = 4;
                return ((bits & checkBit) == checkBit) || isVirtualEStoppedOverriden;
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
            Int64 bits = Convert.ToInt64(URRobotStatus.DigitalInputBits);
            Int64 checkBit = 1;
            return ((bits & checkBit) == checkBit);
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
                    SetFreeDriveMode(true);
                }
                isVirtualEStopped = true;
            }
            else
            {
                if (isVirtualEStopped)
                {
                    SetFreeDriveMode(false);
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
    }
}
