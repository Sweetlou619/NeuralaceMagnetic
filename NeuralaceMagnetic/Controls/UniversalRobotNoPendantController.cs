using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuralaceMagnetic.Controls
{
    public class UniversalRobotNoPendantController : BackgroundWorker
    {
        public bool IsUniversalRobotConnected = false;
        private TcpClient m_URTcpClient;

        private DateTime lastAnalogReadTime = DateTime.MinValue;
        private double AnalogReadValue = double.NaN;

        string commandToWrite = "";
        string lastStatus = "";

        bool powerOnRobot = false;
        public bool IsExpectingPowerOff = true;

        public enum RobotModePendant
        {
            NoController = -1,
            Running = 0,
            Freedrive,
            Ready,
            Initialize,
            SecurityStopped,
            EmergencyStopped,
            Fault,
            NoPower,
            NotConnected,
            Shutdown
        }

        public RobotModePendant RobotMode = UniversalRobotNoPendantController.RobotModePendant.NoController;

        public UniversalRobotNoPendantController()
        {

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
                m_URTcpClient.Connect(App.Current.ApplicationSettings.URIpAddress, 29999);
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

        public string GetLastStatus()
        {
            return lastStatus;
        }

        public void PowerOn()
        {
            IsExpectingPowerOff = false;
            powerOnRobot = true;
            //commandToWrite = "power on\n";
        }

        public void PowerOff()
        {
            IsExpectingPowerOff = true;
            commandToWrite = "power off\n";
        }

        public void BrakeRelease()
        {
            commandToWrite = "brake release\n";
        }

        public void CloseSafetyPopup()
        {
            commandToWrite = "close safety popup\n";
        }

        void GetRobotMode()
        {
            return;
            //write out to tcp
            Stream stream = m_URTcpClient.GetStream();
            Encoding enc = new UnicodeEncoding(true, true, true);
            byte[] arrayBytesAnswer = ASCIIEncoding.ASCII.GetBytes("robotmode\n");
            stream.Write(arrayBytesAnswer, 0, arrayBytesAnswer.Length);

            byte[] allBuffer = new byte[m_URTcpClient.ReceiveBufferSize];
            int k = stream.Read(allBuffer, 0, m_URTcpClient.ReceiveBufferSize);
            string response = Encoding.ASCII.GetString(allBuffer, 0, k);
            string[] lines = response.Split('\n');
            string robotMode = "";
            if (lines.Count() > 1)
            {
                robotMode = lines[lines.Count() - 2];
            }
            try
            {
                int iRobotMode = Convert.ToInt16(robotMode);
                RobotMode = (RobotModePendant)iRobotMode;
            }
            catch { }
        }

        void RunPowerOnSequence()
        {
            //write out to tcp
            Stream stream = m_URTcpClient.GetStream();            
            WriteToSocket("close safety popup\n", ref stream);
            Thread.Sleep(100);
            WriteToSocket("unlock protective stop\n", ref stream);
            Thread.Sleep(100);
            WriteToSocket("power on\n", ref stream);
            Thread.Sleep(5000);
            WriteToSocket("brake release\n", ref stream);
            powerOnRobot = false;
        }

        void WriteToSocket(string command, ref Stream stream)
        {
            Encoding enc = new UnicodeEncoding(true, true, true);
            byte[] arrayBytesAnswer = ASCIIEncoding.ASCII.GetBytes(command);
            stream.Write(arrayBytesAnswer, 0, arrayBytesAnswer.Length);
        }

        void UpdateUniversalRobotStatus()
        {
            if (powerOnRobot)
            {
                RunPowerOnSequence();
            }

            //try and read the last status            
            lock (commandToWrite)
            {
                if (commandToWrite != string.Empty)
                {
                    //write out to tcp
                    Stream stream = m_URTcpClient.GetStream();
                    Encoding enc = new UnicodeEncoding(true, true, true);
                    byte[] arrayBytesAnswer = ASCIIEncoding.ASCII.GetBytes(commandToWrite);
                    stream.Write(arrayBytesAnswer, 0, arrayBytesAnswer.Length);
                    commandToWrite = string.Empty;
                }
            }

            if (m_URTcpClient.Available > 0)
            {
                Stream stream = m_URTcpClient.GetStream();
                byte[] allBuffer = new byte[m_URTcpClient.ReceiveBufferSize];
                int k = stream.Read(allBuffer, 0, m_URTcpClient.ReceiveBufferSize);
                string response = Encoding.ASCII.GetString(allBuffer, 0, k);
                string[] lines = response.Split('\n');
                if (lines.Count() > 1)
                {
                    lastStatus = lines[lines.Count() - 2];
                }
            }

            GetRobotMode();
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

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            DateTime start = new DateTime();
            DateTime stop = new DateTime();
            while (true)
            {
                start = DateTime.Now;
                Thread.Sleep(250);
                if (!IsUniversalRobotConnected)
                {
                    ConnectToUniversalRobotTCPServer();
                }
                else
                {
                    try
                    {
                        UpdateUniversalRobotStatus();
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
