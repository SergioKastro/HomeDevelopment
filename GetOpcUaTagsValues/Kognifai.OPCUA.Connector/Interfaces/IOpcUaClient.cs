using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector.Interfaces
{
    public interface IOpcUaClient
    {
        List<MonitoredItem> CreateMonitoredItems(List<string> listNodeIds);
        void SubscribedMonitoredItems(ICollection<MonitoredItem> items, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback);
        void UnsubscribeMonitorItems(IReadOnlyCollection<MonitoredItem> monitoredItems, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback);
        void Dispose();
        bool Reconnected { get; }
    }
}