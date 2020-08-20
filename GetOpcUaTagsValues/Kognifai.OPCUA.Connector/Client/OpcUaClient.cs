using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Kognifai.OPCUA.Connector.Configuration;
using Kognifai.OPCUA.Connector.Interfaces;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector.Client
{
    public class OpcUaClient : IOpcUaClient
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaClient));

        private readonly OpcUaClientConfiguration _config;
        private readonly AppSettings _appSettings;
        private readonly Action<MonitoredItem, MonitoredItemNotificationEventArgs> _callback;
        private readonly Timer _checkConnectionTimer;
        private MonitoredItemsHandler _monitoredItemsHandler;

        private OpcUaClientSession _sessionClient;
        private Subscription _subscription;
        private List<MonitoredItem> _currentListMonitoredItemsSubscribed;


        public bool Reconnecting { get; set; }
        public bool IsConnected => this._sessionClient.IsConnected;


        public OpcUaClient(AppSettings appSettings, Action<MonitoredItem, MonitoredItemNotificationEventArgs> callback)
        {
            _appSettings = appSettings;
            _callback = callback;
            _currentListMonitoredItemsSubscribed = new List<MonitoredItem>();
            _config = new OpcUaClientConfiguration(appSettings.OpcUaServerUrl);
            _checkConnectionTimer = new Timer
            {
                Interval = TimeSpan.FromMilliseconds(this._appSettings.ConnectionCheckIntervalMs).TotalMilliseconds
            };

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

                await _sessionClient.CreateSessionAsync(_config);

                _monitoredItemsHandler = new MonitoredItemsHandler(_sessionClient, _appSettings);

                StartConnectionCheckTimer();

                SysLog.Info($"Connected to server: { _config.Endpoint.EndpointUrl} .");
            }
            catch (Exception)
            {
                var message = "Failed to connect to server, Please check opcua server is running";

                SysLog.Error(message);
                OnCheckConnectionTimerOnElapsed(this, new EventArgs() as ElapsedEventArgs);                
                //throw new Exception(message);
            }
        }

        public List<MonitoredItem> CreateMonitoredItems(List<string> listNodeIds)
        {
            if (_sessionClient.IsConnected)
            {
                return _monitoredItemsHandler.CreateListMonitoredItems(listNodeIds);
            }

            return new List<MonitoredItem>();
        }

        public bool VerifyIfNodeIdIsValid(string sensorId)
        {
            return _monitoredItemsHandler.VerifyIfNodeIdIsValid(sensorId);
        }

        public void SubscribedMonitoredItems(ICollection<MonitoredItem> items)
        {
            if (_subscription == null || !_subscription.Created)
            {
                return;
            }

            if (items == null || !items.Any())
            {
                return;
            }

            if (_callback != null)
            {
                foreach (var monitoredItem in items)
                {
                    monitoredItem.Notification += _callback.Invoke;
                }
            }

            try
            {
                _subscription.AddItems(items);
                _subscription.ApplyChanges();
            }
            catch (ServiceResultException ex)
            {
                if (ex.StatusCode == StatusCodes.BadRequestTimeout)
                {
                    SysLog.Warn("BadRequestTimeout error message reported from server.");
                }
                else
                {
                    SysLog.Warn("Error reported from server. Message: ", ex);
                    OnCheckConnectionTimerOnElapsed(this, new EventArgs() as ElapsedEventArgs);
                }
            }
            catch (Exception ex)
            {
                SysLog.Error("Unexpected error.", ex);
                OnCheckConnectionTimerOnElapsed(this, new EventArgs() as ElapsedEventArgs);
            }
            finally
            {
                SetCurrentListMonitoredItemsSubscribed(items);
            }
        }

        private void SetCurrentListMonitoredItemsSubscribed(IEnumerable<MonitoredItem> monitoredItems)
        {
            var flattenList = this._currentListMonitoredItemsSubscribed ?? new List<MonitoredItem>();

            foreach (var monitoredItem in monitoredItems)
            {
                if (!flattenList.Contains(monitoredItem))
                {
                    flattenList.Add(monitoredItem);
                }
            }

            this._currentListMonitoredItemsSubscribed = flattenList;

            if (this._currentListMonitoredItemsSubscribed != null && this._currentListMonitoredItemsSubscribed.Any())
            {
                SysLog.Info("\n");
                SysLog.Info($"Current items in the subscription: \n{string.Join("\n", this._currentListMonitoredItemsSubscribed.Select(x => $"{x.DisplayName}({x.StartNodeId})"))}");
                SysLog.Info("\n");
            }
        }

        public void UnsubscribeMonitorItems(IReadOnlyCollection<MonitoredItem> monitoredItems)
        {
            if (monitoredItems == null || !monitoredItems.Any())
            {
                return;
            }

            if (_subscription == null || !_subscription.Created)
            {
                SysLog.Info("Could not remove the following monitoredItem because there is not subscription.");
                SysLog.Info("\n");
                SysLog.Info($"Current items in subscription: \n{string.Join("\n", monitoredItems.Select(x => $"{x.DisplayName}({x.StartNodeId})"))}");
                SysLog.Info("\n");
                return;
            }

            if (_callback != null)
            {
                foreach (var monitoredItem in monitoredItems)
                {
                    monitoredItem.Notification -= _callback.Invoke;
                }
            }

            try
            {
                // Removes an item from the subscription and marks the items for deletion
                _subscription.RemoveItems(monitoredItems);

                // Deletes all items that have been marked for deletion.
                // It needs to have a session alive otherwise it will throw an exception
                _subscription.DeleteItems();

                //Apply all the changes into the subscription. Update the statuses
                _subscription.ApplyChanges();
            }
            catch (ServiceResultException ex)
            {
                if (ex.StatusCode == StatusCodes.BadRequestTimeout)
                {
                    SysLog.Warn("BadRequestTimeout error message reported from server.");
                }
                else
                {
                    SysLog.Warn("Error reported from server. Message: ", ex);
                    OnCheckConnectionTimerOnElapsed(this, new EventArgs() as ElapsedEventArgs);
                }
            }
            catch (Exception ex)
            {
                SysLog.Error("Unexpected error.", ex);
                return;
            }

            _currentListMonitoredItemsSubscribed = null;
        }


        public void CreateSubscription()
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


                //Stop Timer to check connection
                StopTimerCheckConnection();

                //Removing current monitoredItems subscribed
                if (_currentListMonitoredItemsSubscribed == null || !_currentListMonitoredItemsSubscribed.Any() || _subscription == null)
                {
                    SysLog.Info("Removing monitoredItems from Subscription. There are no monitoredItems to be removed from subscription.");
                }
                else
                {
                    SysLog.Info($"Removing {_currentListMonitoredItemsSubscribed.Count} monitoredItems from  subscription: \"{_subscription.DisplayName}\" ...");
                    UnsubscribeMonitorItems(_currentListMonitoredItemsSubscribed);
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

                this.Reconnecting = false;

                SysLog.Info("Disconnected from server.");
            }
        }

        private void StartConnectionCheckTimer()
        {
            this._checkConnectionTimer.Elapsed += OnCheckConnectionTimerOnElapsed;
            this._checkConnectionTimer.Start();
        }

        private void StopTimerCheckConnection()
        {

            this._checkConnectionTimer.Elapsed -= OnCheckConnectionTimerOnElapsed;
            this._checkConnectionTimer.Stop();
        }

        private void Unsubscribe(Subscription subscription)
        {
            if (_subscription == null || !_subscription.Created)
            {
                return;
            }

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
                this.Reconnecting = true;
            }
            else
            {
                this.Reconnecting = false;
            }
        }
    }
}
