using System;

namespace Kognifai.OPCUA.Client.Configuration
{
    public class AppSettings
    {
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
