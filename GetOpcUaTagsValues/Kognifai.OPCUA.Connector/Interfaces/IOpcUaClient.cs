using System.Collections.Generic;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector.Interfaces
{
    public interface IOpcUaClient
    {
        List<MonitoredItem> CreateMonitoredItems(List<string> listNodeIds);
        void SubscribedMonitoredItems(ICollection<MonitoredItem> items);
        void UnsubscribeMonitorItems(IReadOnlyCollection<MonitoredItem> monitoredItems);
        void Dispose();
        bool Reconnecting { get; set; }
        void CreateSubscription();
        bool VerifyIfNodeIdIsValid(string sensorId);
        bool IsConnected{ get; }
    }
}