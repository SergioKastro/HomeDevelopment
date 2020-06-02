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
        private OpcUaClientSession _sessionClient;
        private Subscription _subscription;
        private Timer _checkConnectionTimer;

        public OpcUaClient(AppSettings appSettings)
        {
            _appSettings = appSettings;

            _config = new OpcUaClientConfiguration(appSettings.OpcUaServerUrl);
            CreateOpcuaSessionAsync().GetAwaiter().GetResult();
            CreateSubscription();
        }

        public async Task CreateOpcuaSessionAsync()
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
                _subscription.AddItems(items);
                _subscription.ApplyChanges();
            }
        }

        public void UnsubscribeMonitorItems(ICollection<MonitoredItem> items, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback)
        {
            if (items != null && items.Any())
            {
                foreach (var monitoredItem in items)
                {
                    monitoredItem.Notification -= callback.Invoke;
                }
                _subscription.RemoveItems(items);
                _subscription.ApplyChanges();
            }
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

        public void DisposeSessionClient()
        {
            if (_sessionClient != null)
            {
                SysLog.Info("Disconnecting from server ...");

                //Removing all the subscriptions
                if (_subscription != null)
                {
                    Unsubscribe(_subscription);
                }

                //Close session
                _sessionClient.Close();

                _sessionClient.Dispose();

                SysLog.Info("Disconnected from server.");
            }
        }

        public void Unsubscribe(Subscription subscription)
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

            _checkConnectionTimer.Elapsed += OnCheckConnectionTimerOnElapsedCheckConnection;
        }

        private void OnCheckConnectionTimerOnElapsedCheckConnection(object sender, ElapsedEventArgs e)
        {
            if (_sessionClient == null)
            {
                return;
            }

            if (!_sessionClient.IsConnected)
            {
                SysLog.Warn("Client session disconnected. Trying to resubscribe.");

                this.Dispose(true);
                CreateOpcuaSessionAsync().GetAwaiter().GetResult();
                CreateSubscription();
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
