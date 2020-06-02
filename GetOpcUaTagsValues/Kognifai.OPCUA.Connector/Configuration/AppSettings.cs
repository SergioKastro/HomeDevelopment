using System;

namespace Kognifai.OPCUA.Connector.Configuration
{
    public class AppSettings
    {
        public string OpcUaServerUrl { get; set; }
        public string PrefixFileName { get; set; }
        public string SensorListFilePath { get; set; }
        public string DataFolder { get; set; }
        public int SubscriptionPublishIntervalMs { get; set; }
        public int MonitoredItemsBatchSize { get; set; } = 100; // 100 items per minute(60 sec)
        public int MonitoredItemsBatchIntervalMs { get; set; } = 6000; //Default time: 1 minute in Msc
        public byte SubscriptionPriority { get; set; }
        public uint SubscriptionKeepAliveCount { get; set; }
        public uint SubscriptionLifetimeCount { get; set; }
        public uint SubscriptionMaxNotificationsPerPublish { get; set; }
        public int ServiceIntervalMinutes { get; set; } = 10080; // Default time for the program to run 1 week
        public int ConnectionCheckIntervalMs { get; set; } = 60000; //1 minute = 60000 Msc
        public int DefaultSamplingIntervalMs { get; set; } = 1000;
    }
}
