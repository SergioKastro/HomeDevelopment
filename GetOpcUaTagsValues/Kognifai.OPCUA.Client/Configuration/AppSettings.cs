using System;

namespace Kognifai.OPCUA.Client.Configuration
{
    public class AppSettings
    {
        public string OpcUaServerUrl { get; set; }
        public string PrefixFileName { get; set; }
        public string SensorListFilePath { get; set; }
        public string DataFolder { get; set; }
        public int SubscriptionPublishIntervalMs { get; set; }
        public int? MonitoredItemsBatchSize { get; set; }
        public int? MonitoredItemsBatchIntervalMs { get; set; }
        public TimeSpan? ConnectionCheckInterval { get; set; }
        public byte SubscriptionPriority { get; set; }
        public uint SubscriptionKeepAliveCount { get; set; }
        public uint SubscriptionLifetimeCount { get; set; }
        public uint SubscriptionMaxNotificationsPerPublish { get; set; }
    }
}
