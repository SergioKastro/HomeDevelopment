namespace Kognifai.OPCUA.Connector.Configuration
{
    public class AppSettings
    {
        public string OpcUaServerUrl { get; set; }
        public string SensorListFolderPath { get; set; }
        public string SensorListFileName { get; set; }
        
        public string ResultFolderPath { get; set; }
        public string PrefixResultFileName { get; set; }

        public string PrefixNoLocatedSensorsFileName { get; set; }

        public int SamplingIntervalMs { get; set; } = 1000;
        public int ServiceIntervalMinutes { get; set; } = 10080; // Default time in minutes for the program to run: 1 week = 10080 minutes
        public int ConnectionCheckIntervalMs { get; set; } = 60000; //1 minute = 60000 Msc
        public int MonitoredItemsBatchSize { get; set; } = 20; // Default value 20 items per minute(60 sec)
        public int MonitoredItemsBatchIntervalMs { get; set; } = 60000; //Default time: 1 minute in Msc
        
        public uint SubscriptionKeepAliveCount { get; set; }
        public uint SubscriptionLifetimeCount { get; set; }
        public int SubscriptionPublishIntervalMs { get; set; }
        
    }
}
