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
    public class PolarisCameraController : BackgroundWorker
    {
        class PolarisRigidBodyManager
        {
            private PolarisRigidBody GetAverageFromQueue()
            {
                PolarisRigidBody returnBody = new PolarisRigidBody();
                bool firstIter = true;
                foreach (PolarisRigidBody body in rigidBodyReads)
                {
                    if (firstIter)
                    {
                        firstIter = false;
                        returnBody.x = body.x;
                        returnBody.y = body.y;
                        returnBody.z = body.z;
                        returnBody.qx = body.qx;
                        returnBody.qy = body.qy;
                        returnBody.qz = body.qz;
                        returnBody.qo = body.qo;

                    }
                    else
                    {
                        returnBody.x += body.x;
                        returnBody.y += body.y;
                        returnBody.z += body.z;
                        returnBody.qx += body.qx;
                        returnBody.qy += body.qy;
                        returnBody.qz += body.qz;
                        returnBody.qo += body.qo;
                    }
                }

                returnBody.x = returnBody.x / rigidBodyReads.Count;
                returnBody.y = returnBody.y / rigidBodyReads.Count;
                returnBody.z = returnBody.z / rigidBodyReads.Count;
                returnBody.qx = returnBody.qx / rigidBodyReads.Count;
                returnBody.qy = returnBody.qy / rigidBodyReads.Count;
                returnBody.qz = returnBody.qz / rigidBodyReads.Count;
                returnBody.qo = returnBody.qo / rigidBodyReads.Count;
                returnBody.isInRange = true;
                returnBody.numberOfAverages = rigidBodyReads.Count;
                return returnBody;
            }

            public PolarisRigidBody GetCurrentRead()
            {
                return rigid;
            }

            public void ClearReadings()
            {
                rigidBodyReads.Clear();
                rigid.x = 0;
                rigid.y = 0;
                rigid.z = 0;
                rigid.qx = 0;
                rigid.qy = 0;
                rigid.qz = 0;
                rigid.qo = 0;
                rigid.isInRange = false;
                rigid.numberOfAverages = 0;
            }

            public void PushRBodyReading(PolarisRigidBody rbody)
            {
                rigidBodyReads.Enqueue(rbody);
                if (rigidBodyReads.Count > maxSize)
                {
                    rigidBodyReads.Dequeue();
                }

                rigid = GetAverageFromQueue();
            }

            int maxSize = 10;
            Queue<PolarisRigidBody> rigidBodyReads = new Queue<PolarisRigidBody>();
            PolarisRigidBody rigid = new PolarisRigidBody();
        }

        public struct PolarisRigidBody
        {
            public double x;
            public double y;
            public double z;
            public double qo;
            public double qx;
            public double qy;
            public double qz;
            public bool isInRange;
            public int numberOfAverages;
        }

        private const string kMissingString = "MISSING";

        private TcpClient m_PolarisTcpClient;
        public bool IsPolarisConnected = false;
        private PolarisRigidBodyManager[] RigidBodies = new PolarisRigidBodyManager[3];

        public enum RigidBodyIndex
        {
            Camera = 0,
            UserOne = 1,
            UserTwo = 2,
            UNKNOWN
        }

        public PolarisCameraController()
        {
            for (int i = 0; i < RigidBodies.Length; i++ )
            {
                RigidBodies[i] = new PolarisRigidBodyManager();
            }
            UpdateRigidBodies();
        }

        PolarisRigidBody ConvertRigidBodyIntoURSpace(PolarisRigidBody polaris)
        {
            //positive x is toward the camera
            //positive y is to the right of the camera (camera pov)
            //camera z is out toward the robot 
            //z -950 at closent limit to cam, 2400 is outer limit
            //camera x is up negative up
            //camera y is left and right positive right

            //on rigid bodies ndi must be on top

            //camera y positive is left and right
            PolarisRigidBody converted = new PolarisRigidBody();
            converted.x = (polaris.z / 1000);
            converted.y = (polaris.y / 1000);
            converted.z = ((-polaris.x) / 1000);

            return converted;            
        }

        public PolarisRigidBody GetURRobotRigidBody()
        {
            return GetRigidBody(RigidBodyIndex.Camera);
        }

        public PolarisRigidBody GetUserRigidBodyOne()
        {
            return GetRigidBody(RigidBodyIndex.UserOne);
        }

        public PolarisRigidBody GetUserRigidBodyTwo()
        {
            return GetRigidBody(RigidBodyIndex.UserTwo);
        }

        public PolarisRigidBody GetRigidBody(RigidBodyIndex index)
        {
            return RigidBodies[Convert.ToInt16(index)].GetCurrentRead();
        }

        void ClearRigidBodyData(ref PolarisRigidBodyManager rigid)
        {
            rigid.ClearReadings();
        }

        void UpdateRigidBodies()
        {
            if (!IsPolarisConnected)
            {
                ClearRigidBodyData(ref RigidBodies[0]);
                ClearRigidBodyData(ref RigidBodies[1]);
                ClearRigidBodyData(ref RigidBodies[2]);
            }
        }

        void ConnectToPolarisTCPServer()
        {
            try
            {
                m_PolarisTcpClient = new TcpClient();
                m_PolarisTcpClient.ReceiveTimeout = 1000;
                m_PolarisTcpClient.SendTimeout = 1000;
                m_PolarisTcpClient.Connect(App.Current.ApplicationSettings.CameraIpAddress, 8765);
                IsPolarisConnected = true;
            }
            catch (Exception e)
            {
                Thread.Sleep(1000);
                IsPolarisConnected = false;
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

        double ConvertRotationToDouble(string rotationString)
        {
            string conversionString = rotationString.Substring(1, 1);
            conversionString += ".";
            conversionString += rotationString.Substring(2, 4);
            double absolute = double.Parse(conversionString);
            if (rotationString[0] == '-')
            {
                absolute = -absolute;
            }
            return absolute;
        }

        double ConvertPositionToDouble(string position)
        {
            string conversionString = position.Substring(1, 4);
            conversionString += ".";
            conversionString += position.Substring(4, 2);
            double absolute = double.Parse(conversionString);
            if (position[0] == '-')
            {
                absolute = -absolute;
            }
            return absolute;
        }

        void ParseSinglePolarisString(string bodyString, ref PolarisRigidBodyManager rbodyMan)
        {
            int offset = 2;            
            string qo = bodyString.Substring(offset, 6);            
            offset += 6;
            string qx = bodyString.Substring(offset, 6);            
            offset += 6;
            string qy = bodyString.Substring(offset, 6);
            offset += 6;
            string qz = bodyString.Substring(offset, 6);
            offset += 6;
            string tx = bodyString.Substring(offset, 7);
            offset += 7;
            string ty = bodyString.Substring(offset, 7);
            offset += 7;
            string tz = bodyString.Substring(offset, 7);
            offset += 7;

            PolarisRigidBody rbody;
            rbody.qo = ConvertRotationToDouble(qo);
            rbody.qx = ConvertRotationToDouble(qx);
            rbody.qy = ConvertRotationToDouble(qy);
            rbody.qz = ConvertRotationToDouble(qz);
            rbody.x = ConvertPositionToDouble(tx);
            rbody.y = ConvertPositionToDouble(ty);
            rbody.z = ConvertPositionToDouble(tz);
            rbody.isInRange = true;
            rbody.numberOfAverages = 0;

            rbodyMan.PushRBodyReading(rbody);
        }

        void ParsePolarisResponseString(string response)
        {
            string[] allBodies = response.Split('\n');
            int bodyIndex = 0;
            foreach (string body in allBodies)
            {
                string parseString = body;
                if (bodyIndex == 0)
                {
                    parseString = body.Substring(2);
                }
                if (parseString.Contains("\0"))
                {
                    continue;
                }
                if (parseString.Contains(kMissingString))
                {
                    ClearRigidBodyData(ref RigidBodies[bodyIndex]);
                }
                else
                {
                    ParseSinglePolarisString(parseString, ref RigidBodies[bodyIndex]);
                }
                bodyIndex++;
            }
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            DateTime start = new DateTime();
            DateTime stop = new DateTime();
            TimeSpan span = new TimeSpan();
            while (true)
            {
                span = stop - start;
                span = TimeSpan.FromMilliseconds(50) - span;
                if (span <= TimeSpan.FromMilliseconds(0))
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(8));
                }
                else
                {
                    Thread.Sleep(span);
                }
                start = DateTime.Now;

                if (!IsPolarisConnected)
                {
                    ConnectToPolarisTCPServer();
                }
                else
                {
                    try
                    {
                        Stream stream = m_PolarisTcpClient.GetStream();
                        Encoding enc = new UnicodeEncoding(true, true, true);
                        byte[] arrayBytesAnswer = ASCIIEncoding.ASCII.GetBytes("TX 0001\n");
                        stream.Write(arrayBytesAnswer, 0, arrayBytesAnswer.Length);

                        byte[] bytesToRead = new byte[m_PolarisTcpClient.ReceiveBufferSize];
                        int bytesRead = stream.Read(bytesToRead, 0, m_PolarisTcpClient.ReceiveBufferSize);
                        string convertedAscii = ASCIIEncoding.ASCII.GetString(bytesToRead);
                        ParsePolarisResponseString(convertedAscii);
                    }
                    catch (Exception ex)
                    {
                        IsPolarisConnected = false;
                    }
                }
                stop = DateTime.Now;
                UpdateRigidBodies();
            }
        }
    }
}
