using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace NeuralaceMagnetic.Controls
{
    public class URSecondaryController : BackgroundWorker
    {
        public bool IsUniversalRobotConnected = false;
        private TcpClient m_URTcpClient;

        private DateTime lastAnalogReadTime = DateTime.MinValue;
        private double AnalogReadValue = double.NaN;
        Vector3D LatestAngleInfo = new Vector3D();
        SafetyType currenSafetyType = SafetyType.Unknown;

        bool fireDigitalOutput = false;
        bool lastOnOffCommand = false;
        DateTime lastFireTime = DateTime.MinValue;

        public enum SafetyType
        {
            Unknown = 0,
            Normal = 1,
            Reduced = 2,
            ProtectiveStop = 3,
            Recovery = 4,
            SafegaurdStop = 5,
            SystemEStop = 6,
            RobotEStop = 7,
            Violation = 8,
            Fault = 9
        }

        public URSecondaryController()
        {

        }

        public void FireDigitalOutput()
        {
            fireDigitalOutput = true;
        }

        public void StopFiringDigitalOutput()
        {
            fireDigitalOutput = false;
        }

        private Vector3D GetLatestAngleInfo()
        {
            return LatestAngleInfo;
        }

        public DateTime GetLastAnalogReadTime()
        {
            return lastAnalogReadTime;
        }

        public double GetAnalogValue()
        {
            if ((DateTime.Now - lastAnalogReadTime).TotalMilliseconds > 1000)
            {
                return double.NaN;
            }
            return AnalogReadValue;
        }

        void ConnectToUniversalRobotTCPServer()
        {
            try
            {
                m_URTcpClient = new TcpClient();
                m_URTcpClient.ReceiveTimeout = 1000;
                m_URTcpClient.SendTimeout = 1000;
                //home ur5
                m_URTcpClient.Connect(App.Current.ApplicationSettings.URIpAddress, 30002);
                //shiv ur5
                //m_URTcpClient.Connect("192.168.1.2", 30003);               
                IsUniversalRobotConnected = true;
            }
            catch (Exception e)
            {
                Thread.Sleep(1000);
                IsUniversalRobotConnected = false;
            }
        }

        private void FlipEndian(Type type, byte[] data)
        {
            Array.Reverse(data, 0, Marshal.SizeOf(type));
        }
        void UpdateUniversalRobotStatus()
        {
            Stream stream = m_URTcpClient.GetStream();

            byte[] allBuffer = new byte[m_URTcpClient.ReceiveBufferSize];
            int k = stream.Read(allBuffer, 0, m_URTcpClient.ReceiveBufferSize);

            bool parsingStuff = true;
            if (k < 5)
            {
                parsingStuff = false;
            }
            else
            {
                char c = (char)allBuffer[4];
                parsingStuff = (c == (char)16);
            }

            if (parsingStuff)
            {
                byte[] sub = allBuffer.Take(4).ToArray();
                FlipEndian(typeof(int), sub);
                int size = BitConverter.ToInt32(sub, 0);
                if (size > 0)
                {

                    byte[] singlePacket = allBuffer.Take(size).ToArray();
                    bool parseSubPkt = true;
                    int startOffset = 5;
                    while (parseSubPkt)
                    {
                        byte[] subPacketArr = singlePacket.Skip(startOffset).Take(4).ToArray();
                        FlipEndian(typeof(int), subPacketArr);
                        int subPacketSize = BitConverter.ToInt32(subPacketArr, 0);
                        char packetType = (char)singlePacket[startOffset + 4];
                        if (packetType == (char)2)
                        {
                            //read from the tool
                            //AnalogReadValue = GetDoubleFromPkt(singlePacket, startOffset + 7);
                            //lastAnalogReadTime = DateTime.Now;
                        }
                        else if (packetType == (char)3)
                        {
                            byte[] digitalInput = singlePacket.Skip(startOffset + 5).Take(4).ToArray();
                            FlipEndian(typeof(int), digitalInput);
                            int digitalInputInt = BitConverter.ToInt32(digitalInput, 0);

                            //from analog
                            AnalogReadValue = GetDoubleFromPkt(singlePacket, startOffset + 15);
                            lastAnalogReadTime = DateTime.Now;

                            char safetyType = (char)singlePacket[startOffset + 65];
                            SetSafetyType(safetyType);
                        }
                        else if (packetType == (char)4)
                        {
                            double x = GetDoubleFromPkt(singlePacket, startOffset + 5);
                            double y = GetDoubleFromPkt(singlePacket, startOffset + 5 + (8 * 1));
                            double z = GetDoubleFromPkt(singlePacket, startOffset + 5 + (8 * 2));
                            double rx = GetDoubleFromPkt(singlePacket, startOffset + 5 + (8 * 3));
                            double ry = GetDoubleFromPkt(singlePacket, startOffset + 5 + (8 * 4));
                            double rz = GetDoubleFromPkt(singlePacket, startOffset + 5 + (8 * 5));
                            LatestAngleInfo.X = rx;
                            LatestAngleInfo.Y = ry;
                            LatestAngleInfo.Z = rz;

                            DateTime lastCatesionUpdate = DateTime.Now;
                        }
                        startOffset += subPacketSize;
                        if (startOffset >= size)
                        {
                            parseSubPkt = false;
                        }
                    }
                }
            }
        }

        void SetSafetyType(char c)
        {
            currenSafetyType = (SafetyType)c;
        }

        public SafetyType GetCurrentSafetyStatus()
        {
            return currenSafetyType;
        }

        double GetDoubleFromPkt(byte[] packet, int skip)
        {
            byte[] array = packet.Skip(skip).Take(8).ToArray();
            FlipEndian(typeof(double), array);
            return BitConverter.ToDouble(array, 0);
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

        void SendCommandToRobot(string command)
        {
            Stream stream = m_URTcpClient.GetStream();
            Encoding enc = new UnicodeEncoding(true, true, true);
            byte[] arrayBytesAnswer = ASCIIEncoding.ASCII.GetBytes(command);
            stream.Write(arrayBytesAnswer, 0, arrayBytesAnswer.Length);
        }

        public void SendDigitalCommands()
        {
            if (fireDigitalOutput)
            {
                if ((DateTime.Now - lastFireTime).TotalMilliseconds > 1000)
                {
                    if (lastOnOffCommand)
                    {
                        SendCommandToRobot("set_digital_out(1, True)\n");
                    }
                    else
                    {
                        SendCommandToRobot("set_digital_out(1, False)\n");
                    }
                    lastOnOffCommand = !lastOnOffCommand;
                    lastFireTime = DateTime.Now;
                }
            }
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            DateTime start = new DateTime();
            DateTime stop = new DateTime();
            while (true)
            {
                start = DateTime.Now;
                Thread.Sleep(5);

                if (!IsUniversalRobotConnected)
                {
                    ConnectToUniversalRobotTCPServer();
                }
                else
                {
                    try
                    {
                        UpdateUniversalRobotStatus();
                        SendDigitalCommands();
                    }
                    catch (Exception ex)
                    {
                        IsUniversalRobotConnected = false;
                    }
                }
                stop = DateTime.Now;
            }
        }
    }
}
