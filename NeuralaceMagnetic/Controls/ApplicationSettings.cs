using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace NeuralaceMagnetic.Controls
{
    public class ApplicationSettings
    {
        public class ApplicationSettingsXML
        {
            public double TherapyPassOneJumpMM = 9;
            public double TherapyPassTwoJumpMM = 3;
            public double MaximumTrackingDistanceMM = 100;
            public double CoilToTargetDistanceMM = 5;
            public double CalibrationTreatmentTimeS = 2;
            public string URIpAddress = "192.168.1.2";
            public string CameraIpAddress = "192.168.1.10";
            public double MaxTrackingTimeWindowMS = 300;
            public double MaxTrackingMovePerWindowMM = 50;
            public bool TrackTOFSensor = true;
            public double TOFDistance = 100;
            public string ForceSensorIpAddress = "192.168.1.11";
            public double ForceSensorThresholdNewtons = 10;
            public double ForceRetractDistanceMM = 25;
        }

        public double TherapyPassOneJumpMM = 9;
        public double TherapyPassTwoJumpMM = 3;
        public double MaximumTrackingDistanceMM = 100;
        public double CoilToTargetDistanceMM = 5;
        public double CalibrationTreatmentTimeS = 2;
        public string URIpAddress = "192.168.1.2";
        public string CameraIpAddress = "192.168.1.10";
        public double MaxTrackingTimeWindowMS = 300;
        public double MaxTrackingMovePerWindowMM = 50;
        public bool TrackTOFSensor = true;
        public double TOFDistance = 100;
        public string ForceSensorIpAddress = "192.168.1.11";
        public double ForceSensorThresholdNewtons = 10;
        public double ForceRetractDistanceMM = 25;

        public ApplicationSettings()
        {
            LoadFromFile();
        }

        public ApplicationSettings(bool loadFromFile)
        {
            if (loadFromFile)
            { 
                LoadFromFile();
            }
        }

        private ApplicationSettingsXML LoadConfig(string path)
        {
            ApplicationSettingsXML configFile = null;
            try
            {
                if (System.IO.File.Exists(path))
                {
                    // Create an instance of the XmlSerializer specifying type and namespace.
                    XmlSerializer serializer = new XmlSerializer(typeof(ApplicationSettingsXML));

                    // A FileStream is needed to read the XML document.
                    FileStream fs = new FileStream(path, FileMode.Open);
                    XmlReader reader = new XmlTextReader(fs);


                    // Use the Deserialize method to restore the object's state.
                    configFile = (ApplicationSettingsXML)serializer.Deserialize(reader);

                    reader.Close();

                    return configFile;
                }
                else
                {
                    return configFile;
                }
            }
            catch
            {
                return configFile;
            }
        }

        private bool WriteConfig(string path, ApplicationSettingsXML configFile)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ApplicationSettingsXML));

                // Create an XmlTextWriter using a FileStream.
                Stream fs = new FileStream(path, FileMode.Create);
                XmlTextWriter writer =
                new XmlTextWriter(fs, Encoding.Unicode);
                writer.Formatting = Formatting.Indented;
                // Serialize using the XmlTextWriter.
                serializer.Serialize(writer, configFile);
                writer.Close();

                return true;
            }
            catch (Exception ex) { return false; }
        }

        public void LoadFromFile()
        {
            ApplicationSettingsXML appConfig = LoadConfig(Directory.GetCurrentDirectory() + "\\Config.xml");
            if (appConfig != null)
            {
                this.TherapyPassOneJumpMM = appConfig.TherapyPassOneJumpMM;
                this.TherapyPassTwoJumpMM = appConfig.TherapyPassTwoJumpMM;
                this.MaximumTrackingDistanceMM = appConfig.MaximumTrackingDistanceMM;
                this.CoilToTargetDistanceMM = appConfig.CoilToTargetDistanceMM;
                this.CalibrationTreatmentTimeS = appConfig.CalibrationTreatmentTimeS;
                this.URIpAddress = appConfig.URIpAddress;
                this.CameraIpAddress = appConfig.CameraIpAddress;
                this.MaxTrackingTimeWindowMS = appConfig.MaxTrackingTimeWindowMS;
                this.MaxTrackingMovePerWindowMM = appConfig.MaxTrackingMovePerWindowMM;
                this.TrackTOFSensor = appConfig.TrackTOFSensor;
                this.TOFDistance = appConfig.TOFDistance;
                this.ForceSensorIpAddress = appConfig.ForceSensorIpAddress;
                this.ForceSensorThresholdNewtons = appConfig.ForceSensorThresholdNewtons;
                this.ForceRetractDistanceMM = appConfig.ForceRetractDistanceMM;
            }
        }

        public void SaveToFile()
        {
            ApplicationSettingsXML appConfig = new ApplicationSettingsXML();
            appConfig.TherapyPassOneJumpMM = this.TherapyPassOneJumpMM;
            appConfig.TherapyPassTwoJumpMM = this.TherapyPassTwoJumpMM;
            appConfig.MaximumTrackingDistanceMM = this.MaximumTrackingDistanceMM;
            appConfig.CoilToTargetDistanceMM = this.CoilToTargetDistanceMM;
            appConfig.CalibrationTreatmentTimeS = this.CalibrationTreatmentTimeS;
            appConfig.URIpAddress = this.URIpAddress;
            appConfig.CameraIpAddress = this.CameraIpAddress;
            appConfig.MaxTrackingTimeWindowMS = this.MaxTrackingTimeWindowMS;
            appConfig.MaxTrackingMovePerWindowMM = this.MaxTrackingMovePerWindowMM;
            appConfig.TrackTOFSensor = this.TrackTOFSensor;
            appConfig.TOFDistance = this.TOFDistance;
            appConfig.ForceSensorIpAddress = this.ForceSensorIpAddress;
            appConfig.ForceSensorThresholdNewtons = this.ForceSensorThresholdNewtons;
            appConfig.ForceRetractDistanceMM = this.ForceRetractDistanceMM;
            WriteConfig(Directory.GetCurrentDirectory() + "\\Config.xml", appConfig);

        }
    }
}
