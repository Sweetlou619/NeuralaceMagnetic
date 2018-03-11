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

namespace NeuralaceMagnetic.Controls
{
    public class ForceTorqueSensorController : BackgroundWorker
    {
        public struct ForceReadout
        {
            public double XForce;
            public double YForce;
            public double ZForce;
            public double XTorque;
            public double YTorque;
            public double ZTorque;
        }

        public bool IsSensorConnected = false;
        private TcpClient m_TcpClient;
        ForceReadout lastForceRead;
        DateTime lastForceReadTime = DateTime.MinValue;

        public ForceTorqueSensorController()
        {

        }

        void ConnectToTorqueSensorTCPServer()
        {
            try
            {
                m_TcpClient = new TcpClient();
                m_TcpClient.ReceiveTimeout = 1000;
                m_TcpClient.SendTimeout = 1000;
                m_TcpClient.Connect(App.Current.ApplicationSettings.ForceSensorIpAddress, 49151);
                IsSensorConnected = true;
            }
            catch (Exception e)
            {
                Thread.Sleep(1000);
                IsSensorConnected = false;
            }
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

        void WriteOutMessage(byte message)
        {
            Stream stream = m_TcpClient.GetStream();
            Encoding enc = new UnicodeEncoding(true, true, true);
            byte[] arrayBytesAnswer = new byte[20];
            for (int i = 0; i < 20; i++)
            {
                arrayBytesAnswer[i] = 0;
            }
            arrayBytesAnswer[0] = message;
            stream.Write(arrayBytesAnswer, 0, arrayBytesAnswer.Length);
        }

        private void FlipEndian(Type type, byte[] data)
        {
            Array.Reverse(data, 0, Marshal.SizeOf(type));
        }

        Int16 GetIntFromPkt(byte[] packet, int skip)
        {
            byte[] array = packet.Skip(skip).Take(2).ToArray();
            FlipEndian(typeof(Int16), array);
            return BitConverter.ToInt16(array, 0);
        }

        Int32 GetInt32FromPkt(byte[] packet, int skip)
        {
            byte[] array = packet.Skip(skip).Take(4).ToArray();
            FlipEndian(typeof(Int32), array);
            return BitConverter.ToInt32(array, 0);
        }

        public ForceReadout GetForceReadOut()
        {
            if ((DateTime.Now - lastForceReadTime).TotalMilliseconds > 1000)
            {
                return new ForceReadout() { XForce = 0, YForce = 0, ZForce = 0 };
            }
            return lastForceRead;
        }

        int ReadInputResponse(ref byte[] array)
        {
            Stream stream = m_TcpClient.GetStream();
            array = new byte[m_TcpClient.ReceiveBufferSize];
            return stream.Read(array, 0, m_TcpClient.ReceiveBufferSize);
        }

        void ReadForceFromSensor()
        {
            WriteOutMessage(1);
            byte[] buffer = new byte[1];
            int size = ReadInputResponse(ref buffer);
            if (size == 24)
            {
                sbyte force  = Convert.ToSByte(buffer[2]);
                sbyte torque = Convert.ToSByte(buffer[3]);
                double cpf    = GetInt32FromPkt(buffer, 4);
                double cpt = GetInt32FromPkt(buffer, 8);
                double scale1 = GetIntFromPkt(buffer, 12);
                double scale2 = GetIntFromPkt(buffer, 14);
                double scale3 = GetIntFromPkt(buffer, 16);
                double scale4 = GetIntFromPkt(buffer, 18);
                double scale5 = GetIntFromPkt(buffer, 20);
                double scale6 = GetIntFromPkt(buffer, 22);

                WriteOutMessage(0);
                size = ReadInputResponse(ref buffer);
                if (size == 16)
                {
                    double XForce = GetIntFromPkt(buffer, 4);
                    double YForce = GetIntFromPkt(buffer, 6);
                    double ZForce = GetIntFromPkt(buffer, 8);
                    double XTorque = GetIntFromPkt(buffer, 10);
                    double YTorque = GetIntFromPkt(buffer, 12);
                    double ZTorque = GetIntFromPkt(buffer, 14);

                    lastForceRead = new ForceReadout();
                    lastForceRead.XForce = XForce * scale1 / cpf;
                    lastForceRead.YForce = YForce * scale2 / cpf;
                    lastForceRead.ZForce = ZForce * scale3 / cpf;
                    lastForceRead.XTorque = XTorque * scale4 / cpt;
                    lastForceRead.YTorque = YTorque * scale5 / cpt;
                    lastForceRead.ZTorque = ZTorque * scale6 / cpt;
                    lastForceReadTime = DateTime.Now;
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

                if (!IsSensorConnected)
                {
                    ConnectToTorqueSensorTCPServer();
                }
                else
                {
                    try
                    {
                        ReadForceFromSensor();
                    }
                    catch (Exception ex)
                    {
                        IsSensorConnected = false;
                    }
                    
                }
                stop = DateTime.Now;
            }
        }
    }
}
