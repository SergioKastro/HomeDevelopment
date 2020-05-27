using Kognifai.OPCUA.Client.Configuration;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Text;

namespace Kognifai.OPCUA.Client.Client
{
    public class OpcUaClientSubscription
    {
        private Subscription _subscription;
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaClient));
        private readonly AppSettings _appSettings;
        private bool IsSubscriptionTypePublishing { get; set; }

        public bool IsMatrikonServer { get; set; }
        public OpcUaClientSubscription(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        public Subscription CreateSubscription(OpcUaClientSession session)
        {
            SysLog.Info("Creating new subscription");

            IsSubscriptionTypePublishing = true;

            _subscription = new Subscription
            {
                DisplayName = Constants.DefaultSubscriptionName,
                PublishingInterval = _appSettings.SubscriptionPublishIntervalMs,
                TimestampsToReturn = TimestampsToReturn.Both,
                Priority = _appSettings.SubscriptionPriority,
                KeepAliveCount = _appSettings.SubscriptionKeepAliveCount,
                LifetimeCount = _appSettings.SubscriptionLifetimeCount,
                MaxNotificationsPerPublish = _appSettings.SubscriptionMaxNotificationsPerPublish
            };

            session.AddSubscription(_subscription);

            _subscription.Create();
            _subscription.SetPublishingMode(IsSubscriptionTypePublishing);

            if (IsSubscriptionTypePublishing)
            {
                _subscription.PublishStatusChanged += OnPublishStatusChanged;
            }
            else
            {
                SysLog.Info(GetDisplayString(_subscription));
            }


            SysLog.Info("Created new subscription");
            return _subscription;
        }

        private void OnPublishStatusChanged(object sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, _subscription))
            {
                return;
            }

            try
            {
                if (_subscription == null || _subscription.PublishingStopped)
                {
                    SysLog.Error("OPCUA Subscription STOPPED.");
                }
                else
                {
                    SysLog.Info(GetDisplayString(_subscription));
                }
            }
            catch (Exception ex)
            {
                const string message = "Error in Subscription handler";

                if (SysLog.IsDebugEnabled)
                    SysLog.Error(message, ex);
                else
                    SysLog.Error(message);
            }
        }

        private string GetDisplayString(Subscription subscription)
        {
            var buffer = new StringBuilder();

            buffer.Append("\n Subscription Name: " + subscription.DisplayName + " ");
            buffer.Append("\n Subscription Mode/type: ");
            buffer.Append(subscription.CurrentPublishingEnabled ? "Publishing" : "Polling");
            buffer.Append(subscription.CurrentPublishingEnabled ? "\n PublishingTime:" : "\n PollingTime:");
            buffer.Append(_subscription.PublishTime.ToLocalTime().ToString("hh:mm:ss"));
            buffer.Append("\n CurrentSubscriptionPublishingInterval: ");
            buffer.Append(subscription.CurrentPublishingInterval / 1000 + @"s");
            buffer.Append("\n CurrentKeepAliveCount (Seconds before an empty notification is sent anyway): ");
            buffer.Append(subscription.CurrentPublishingInterval * subscription.CurrentKeepAliveCount / 1000 + @"s");
            buffer.Append("\n CurrentLifetimeCount (Seconds before the server realized the client is no longer active): ");
            buffer.Append(subscription.CurrentPublishingInterval * subscription.CurrentLifetimeCount / 1000 + @"s");
            buffer.Append("\n NumberMonitoredItems: ");
            buffer.Append(_subscription.MonitoredItemCount.ToString());
            buffer.Append("\n SequenceNumber: ");
            buffer.Append(_subscription.SequenceNumber.ToString());


            return buffer.ToString();
        }
    }
}
