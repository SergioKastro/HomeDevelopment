using Kognifai.OPCUA.Client.Configuration;
using log4net;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kognifai.OPCUA.Client.Client
{
    public class OpcUaClient
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaClient));

        private readonly OpcUaClientConfiguration _config;
        private readonly AppSettings _appSettings;
        private OpcUaClientSession _sessionClient;
        private List<Subscription> _listSubscriptions;

        public OpcUaClient(AppSettings appSettings)
        {
            _config = new OpcUaClientConfiguration(appSettings.OpcUaServerUrl);
            _listSubscriptions = new List<Subscription>();
            CreateOpcuaSessionAsync().GetAwaiter().GetResult();
            _appSettings = appSettings;
        }

        public async Task CreateOpcuaSessionAsync()
        {
            try
            {
                if (_sessionClient == null)
                {
                    _sessionClient = new OpcUaClientSession();
                }

                await _sessionClient.CreateSessionAsync(_config);

                SysLog.Info($"Connected to server: { _config.Endpoint.EndpointUrl} .");
            }
            catch (Exception ex)
            {
                SysLog.Error("Failed to connect to server, Please check opcua server is running", ex);
            }
        }

        public void SubscribedMonitoredItems(List<string> listNodeIds)
        {
            var opcSubscription = CreateSubscription();

            var monitoredItemsHandler = new MonitoredItemsHandler(_sessionClient, _appSettings);
            var listMonitoredItems = monitoredItemsHandler.CreateListMonitoredItems(listNodeIds);

            opcSubscription.AddItems(listMonitoredItems);
            opcSubscription.ApplyChanges();
        }

        private Subscription CreateSubscription()
        {
            var opcUaClientSubscription = new OpcUaClientSubscription(_appSettings);

            var susbcription = opcUaClientSubscription.CreateSubscription(_sessionClient);

            _listSubscriptions.Add(susbcription);

            return susbcription;
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

                //Removing all the subscriptions
                CancelAllSubscriptions();

                //Close session
                _sessionClient.Close();

                _sessionClient.Dispose();

                SysLog.Info("Disconnected from server.");
            }
        }

        private void CancelAllSubscriptions()
        {
            while (_listSubscriptions.Any())
            {
                var sub = _listSubscriptions.First();
                Unsubscribe(sub);
            }
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
                _listSubscriptions.Remove(subscription);
            }
            catch (Exception ex)
            {
                SysLog.Warn("Failed to clear subscription from subscription list.", ex);
            }

            SysLog.Info($"Unsubscribed from \"{subscription.DisplayName}\".");
        }
    }
}
