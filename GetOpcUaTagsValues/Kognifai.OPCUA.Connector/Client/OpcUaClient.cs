using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Kognifai.OPCUA.Connector.Configuration;
using Kognifai.OPCUA.Connector.Interfaces;
using log4net;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector.Client
{
    public class OpcUaClient : IOpcUaClient
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaClient));

        private readonly OpcUaClientConfiguration _config;
        private readonly AppSettings _appSettings;
        private readonly Action<MonitoredItem, MonitoredItemNotificationEventArgs> _callback;
        private List<MonitoredItem> _listMonitoredItemsSubscribedPerSubscription;

        private OpcUaClientSession _sessionClient;
        private Subscription _subscription;
        private Timer _checkConnectionTimer;


        public OpcUaClient(AppSettings appSettings, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback)
        {
            _appSettings = appSettings;
            _callback = callback;
            _listMonitoredItemsSubscribedPerSubscription = new List<MonitoredItem>();
            _config = new OpcUaClientConfiguration(appSettings.OpcUaServerUrl);

            InitOpcUaClient();
        }

        private void InitOpcUaClient()
        {
            CreateOpcuaSessionAsync().GetAwaiter().GetResult();
            CreateSubscription();
        }

        private async Task CreateOpcuaSessionAsync()
        {
            try
            {
                if (_sessionClient == null)
                {
                    _sessionClient = new OpcUaClientSession();
                }

                StartConnectionCheckTimer();

                await _sessionClient.CreateSessionAsync(_config);

                SysLog.Info($"Connected to server: { _config.Endpoint.EndpointUrl} .");
            }
            catch (Exception ex)
            {
                SysLog.Error("Failed to connect to server, Please check opcua server is running", ex);
            }
        }

        public List<MonitoredItem> CreateMonitoredItems(List<string> listNodeIds)
        {
            if (!_sessionClient.IsConnected)
            {
                SysLog.Error("Failed to connect to server, Please check opcua server is running");
                return null;
            }

            var monitoredItemsHandler = new MonitoredItemsHandler(_sessionClient, _appSettings);
            return monitoredItemsHandler.CreateListMonitoredItems(listNodeIds);
        }

        public void SubscribedMonitoredItems(ICollection<MonitoredItem> items, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback)
        {
            if (items != null && items.Any())
            {
                foreach (var monitoredItem in items)
                {
                    monitoredItem.Notification += callback.Invoke;
                }

                try
                {
                    _subscription.AddItems(items);
                    _subscription.ApplyChanges();
                }
                catch (Exception ex)
                {
                    SysLog.Error("Unexpected error.", ex);
                    return;
                }

                var currentListMonitoredItemsSubscribed = CreateCurrentListMonitoredItemsSubscribed(items);

                _listMonitoredItemsSubscribedPerSubscription = currentListMonitoredItemsSubscribed;
            }
        }

        private List<MonitoredItem> CreateCurrentListMonitoredItemsSubscribed(IEnumerable<MonitoredItem> monitoredItems)
        {
            var flattenList = _listMonitoredItemsSubscribedPerSubscription ?? new List<MonitoredItem>();

            foreach (var monitoredItem in monitoredItems)
            {
                if (!flattenList.Contains(monitoredItem))
                {
                    flattenList.Add(monitoredItem);
                }
            }

            return flattenList;
        }

        public void UnsubscribeMonitorItems(IReadOnlyCollection<MonitoredItem> monitoredItems, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback)
        {
            if (monitoredItems == null || !monitoredItems.Any())
            {
                SysLog.Warn("There are not monitoredItems to be removed from subscription.");
                return;
            }

            if (_subscription == null)
            {
                SysLog.Warn("Could not remove any monitoredItem because there is not subscription.");
                return;
            }

            foreach (var monitoredItem in monitoredItems)
            {
                monitoredItem.Notification -= callback.Invoke;
            }

            try
            {
                _subscription.RemoveItems(monitoredItems);
                _subscription.ApplyChanges();
            }
            catch (Exception ex)
            {
                SysLog.Error("Unexpected error.", ex);
                return;
            }

            _listMonitoredItemsSubscribedPerSubscription = null;
        }


        private void CreateSubscription()
        {
            var opcUaClientSubscription = new OpcUaClientSubscription(_appSettings);
            _subscription = opcUaClientSubscription.CreateSubscription(_sessionClient);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            DisposeSessionClient();
        }

        private void DisposeSessionClient()
        {
            if (_sessionClient != null)
            {
                SysLog.Info("Disconnecting from server ...");

                //Removing current monitoredItems subscribed
                if (_listMonitoredItemsSubscribedPerSubscription != null && _listMonitoredItemsSubscribedPerSubscription.Any() && _subscription != null)
                {
                    SysLog.Info($"Removing {_listMonitoredItemsSubscribedPerSubscription.Count} monitoredItems from  subscription: \"{_subscription.DisplayName}\" ...");
                    UnsubscribeMonitorItems(_listMonitoredItemsSubscribedPerSubscription, _callback);
                    SysLog.Info($"Removed ALL monitoredItems from  subscription: \"{_subscription.DisplayName}\".");
                }

                //Removing active subscription
                if (_subscription != null)
                {
                    Unsubscribe(_subscription);
                }

                //Close session
                _sessionClient.Close();

                _sessionClient.Dispose();

                //Stop Timer to check connection
                StopTimerCheckConnection();

                SysLog.Info("Disconnected from server.");
            }
        }

        public void StopTimerCheckConnection()
        {
            _checkConnectionTimer.Elapsed -= OnCheckConnectionTimerOnElapsed;
            _checkConnectionTimer.Stop();
        }

        private void Unsubscribe(Subscription subscription)
        {
            if (subscription == null)
                return;

            SysLog.Info($"Unsubscribing \"{subscription.DisplayName}\" ...");

            try
            {
                _sessionClient.RemoveSubscription(subscription);
                SysLog.Info("Subscription successfully removed from session.");
            }
            catch (Exception ex)
            {
                SysLog.Warn("Failed to remove subscription from session.", ex);
            }

            SysLog.Info("Disposing of the subscription ...");
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                SysLog.Warn("Failed to clear subscription from subscription list.", ex);
            }

            SysLog.Info($"Unsubscribed from \"{subscription.DisplayName}\".");
        }


        private void StartConnectionCheckTimer()
        {

            _checkConnectionTimer = new Timer
            {
                AutoReset = true,
                Enabled = true,
                Interval = TimeSpan.FromMilliseconds(this._appSettings.ConnectionCheckIntervalMs).TotalMilliseconds
            };

            _checkConnectionTimer.Elapsed += OnCheckConnectionTimerOnElapsed;
        }

        private void OnCheckConnectionTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (_sessionClient == null)
            {
                return;
            }

            if (!_sessionClient.IsConnected)
            {
                SysLog.Warn("Client session disconnected. Trying to reconnect.");

                this.Dispose(true);
                InitOpcUaClient();
                Reconnected = _sessionClient.IsConnected;
            }
            else
            {
                Reconnected = false;
            }
        }

        public bool Reconnected { get; internal set; }


    }
}
