using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ISCSIConsole
{
    [Serializable]
    public class AutoStartConfiguration
    {
        public bool StartServerOnLaunch { get; set; }
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public List<TargetConfiguration> Targets { get; set; }

        public AutoStartConfiguration()
        {
            StartServerOnLaunch = false;
            IPAddress = "0.0.0.0";
            Port = 3260;
            Targets = new List<TargetConfiguration>();
        }
    }

    [Serializable]
    public class TargetConfiguration
    {
        public string IQN { get; set; }
        public List<string> DiskImagePaths { get; set; }

        public TargetConfiguration()
        {
            IQN = String.Empty;
            DiskImagePaths = new List<string>();
        }
    }

    public static class AutoStartConfigurationStore
    {
        public static string ConfigurationDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "iSCSIConsole");
            }
        }

        public static string ConfigurationPath
        {
            get { return Path.Combine(ConfigurationDirectory, "autostart.xml"); }
        }

        public static AutoStartConfiguration Load()
        {
            if (!File.Exists(ConfigurationPath))
            {
                return null;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(AutoStartConfiguration));
            using (FileStream stream = File.OpenRead(ConfigurationPath))
            {
                return (AutoStartConfiguration)serializer.Deserialize(stream);
            }
        }

        public static void Save(AutoStartConfiguration configuration)
        {
            Directory.CreateDirectory(ConfigurationDirectory);
            XmlSerializer serializer = new XmlSerializer(typeof(AutoStartConfiguration));
            using (FileStream stream = File.Create(ConfigurationPath))
            {
                serializer.Serialize(stream, configuration);
            }
        }
    }
}
