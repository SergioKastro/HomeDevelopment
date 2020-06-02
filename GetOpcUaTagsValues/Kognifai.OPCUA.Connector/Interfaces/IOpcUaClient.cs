using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector.Interfaces
{
    public interface IOpcUaClient
    {
        Task CreateOpcuaSessionAsync();
        List<MonitoredItem> CreateMonitoredItems(List<string> listNodeIds);
        void SubscribedMonitoredItems(ICollection<MonitoredItem> items, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback);
        void UnsubscribeMonitorItems(ICollection<MonitoredItem> items, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback);
        void Dispose();
        void DisposeSessionClient();
        void Unsubscribe(Subscription subscription);
        bool Reconnected { get; }
    }
}