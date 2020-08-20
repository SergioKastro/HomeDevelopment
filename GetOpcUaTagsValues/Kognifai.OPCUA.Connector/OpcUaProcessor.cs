using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kognifai.File;
using Kognifai.OPCUA.Connector.Client;
using Kognifai.OPCUA.Connector.Configuration;
using Kognifai.OPCUA.Connector.Interfaces;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector
{
    public sealed class OpcUaProcessor : IOpcUaProcessor
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaProcessor));
        private static readonly object LockObject = new object();

        private readonly AppSettings _appSettings;
        private readonly ConcurrentDictionary<NodeId, MonitoredItem> _queue = new ConcurrentDictionary<NodeId, MonitoredItem>();
        private readonly Dictionary<NodeId, (MonitoredItem Item, MonitorItemStatus Status)> _transmittedItems = new Dictionary<NodeId, (MonitoredItem Item, MonitorItemStatus Status)>();
        private readonly TimeSpan _monitoredItemsBatchIntervalMs;
        private readonly int _maxMonitoredItemsBatchSize;
        private readonly Stopwatch _processorElapsedTime;
        private readonly MessageBuilder _messageBuilder;

        private IOpcUaClient _client;
        private string _fileName;
        private int _totalItemsToAddInCurrentIteration;
        private CancellationTokenSource _cancellationTokenSource;
        private List<MonitoredItem> _monitoredItemsAddedLastIterationInSubscription;
        private DateTime _nextIterationTimeToAddItemsIntoSubscription;
        private List<MonitoredItem> _itemsToBeUnsubscribed;
        
        
        public bool IsRunning { get; private set; }

        public event EventHandler ReconnectingEventHandler;

        public int TotalItemsToAddInCurrentIteration
        {
            get => _totalItemsToAddInCurrentIteration;
            set => _totalItemsToAddInCurrentIteration = value > this._maxMonitoredItemsBatchSize ?
                this._maxMonitoredItemsBatchSize :
                value;
        }

        private void OnReconnecting(EventArgs e)
        {
            SysLog.Info("Calling OnReconnecting event handler. Reconnecting...");
            
            var handler = this.ReconnectingEventHandler;
            handler?.Invoke(this, e);
            this._client.Reconnecting = false;//Clear this flag to avoid calling again reconnect when starting this processor
        }

        private enum MonitorItemStatus
        {
            Pending,
            Completed
        }

        public OpcUaProcessor(AppSettings appSettings)
        {
            this._appSettings = appSettings;
            this._maxMonitoredItemsBatchSize = appSettings.MonitoredItemsBatchSize;
            this._monitoredItemsBatchIntervalMs = TimeSpan.FromMilliseconds(appSettings.MonitoredItemsBatchIntervalMs);
            this.IsRunning = false;
            this._processorElapsedTime = new Stopwatch();
            this._messageBuilder = new MessageBuilder();
        }

        public void Start()
        {
            if (!this.IsRunning)
            {
                try
                {
                    SysLog.Info("Starting OpcUa Processor ....");

                    this._processorElapsedTime.Start();

                    this._client = new OpcUaClient(_appSettings, this.Notify);

                    if (this._client.IsConnected)
                    {
                        CreateListMonitoredItemsFromFile(this._client);
                        this._cancellationTokenSource = new CancellationTokenSource();
                        this.IsRunning = true;

                        //Run the tasks
                        Task.WhenAny(
                                this.AddItemsToSubscriptionTask(this._cancellationTokenSource.Token),
                                this.MonitorTask(this._cancellationTokenSource.Token))
                            .Wait();

                        //When completed we stop the whole process
                        SysLog.Info("\n\n OpcUa Processor completed. Stopping and waiting for next interval.\n\n");
                    }

                }
                catch (Exception ex)
                {
                    SysLog.Error("OpcUa Processor unexpected error. Stopping and waiting for next interval.", ex);
                }
                finally
                {
                    this.Stop();

                    if (!this._client.IsConnected || this._client.Reconnecting)
                    {
                        OnReconnecting(EventArgs.Empty);
                    }
                }
            }
        }

        private void CreateListMonitoredItemsFromFile(IOpcUaClient client)
        {
            SysLog.Debug("Starting to create list of monitoredItems.");
            this._fileName = this._appSettings.PrefixResultFileName + $"{DateTime.Now:yyyy_MM_dd_HH_mm_ss}" + ".csv";
            var listSensors = FileManager.DataReading(Path.Combine(this._appSettings.SensorListFolderPath, this._appSettings.SensorListFileName));

            lock (LockObject)
            {
                if (client != null)
                {
                    var items = client.CreateMonitoredItems(listSensors);

                    foreach (var transmittedItem in items)
                    {
                        this._transmittedItems[transmittedItem.StartNodeId] = (transmittedItem, MonitorItemStatus.Pending);
                    }
                }

                // initialize items to add
                this.TotalItemsToAddInCurrentIteration = this._maxMonitoredItemsBatchSize;
                // initialize itemsToUnsubscribed
                this._itemsToBeUnsubscribed = new List<MonitoredItem>();
            }

            SysLog.Debug("Finished creating list of monitoredItems.\n");
        }

        public void Stop()
        {
            //Stop timer for elapse time
            this._processorElapsedTime.Stop();

            if (this.IsRunning)
            {
                SysLog.Info("Stopping OpcUa processor ....");

                //Remove monitoredItems from the Subscription
                var allPendingItems = this._transmittedItems
                    .Where(i => i.Value.Status == MonitorItemStatus.Pending)
                    .Select(i => i.Value.Item)
                    .ToList();

                this._client.UnsubscribeMonitorItems(allPendingItems);

                //Clear the concurrent queue
                this._queue.Clear();

                //Close the subscription and the client session
                this._client.Dispose();

                //Cancel all the tasks
                this._cancellationTokenSource.Cancel();

                this.IsRunning = false;
            }

            SysLog.Info("OpcUa processor stopped.");
        }

        public void Shutdown()
        {
            this.Stop();
        }

        private async Task AddItemsToSubscriptionTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //added ref
                lock (LockObject)
                {
                    this._monitoredItemsAddedLastIterationInSubscription = this._transmittedItems
                        .Where(i => i.Value.Status == MonitorItemStatus.Pending)
                        .Select(i => i.Value.Item)
                        .Take(this.TotalItemsToAddInCurrentIteration)
                        .ToList();

                    this._client.SubscribedMonitoredItems(this._monitoredItemsAddedLastIterationInSubscription);

                    this._itemsToBeUnsubscribed.Clear();
                    this.TotalItemsToAddInCurrentIteration = 0;
                }

                //We delay here to have control how many items we are adding per interval:
                //_totalItemsToAddPerIteration per _monitoredItemsBatchIntervalMs (100 items per minute)
                this._nextIterationTimeToAddItemsIntoSubscription = DateTime.Now.AddMilliseconds(this._monitoredItemsBatchIntervalMs.TotalMilliseconds);
                await Task.Delay(this._monitoredItemsBatchIntervalMs, cancellationToken);

                MarkAndRemoveInvalidNodes();

                if (this.IsCompleted || this._client.Reconnecting)
                {
                    this._cancellationTokenSource.Cancel();
                }
            }
        }

        private void MarkAndRemoveInvalidNodes()
        {
            // After 3 or 5 minutes if we didn't get the value of a NodeId then probably is not a valid sensor
            // Then we will look in the last iteration list of monitoredItems and if one of them is still pending
            // then we will remove it if it is not valid.
            // We will mark the NodeId as Completed, and also we will write on the file for not valid NodeIds.
            foreach (var node in _monitoredItemsAddedLastIterationInSubscription.Select(monitoredItem => this._transmittedItems
                .Where(i => i.Key == monitoredItem.StartNodeId && i.Value.Status == MonitorItemStatus.Pending)
                .Select(i => i.Value.Item)
                .FirstOrDefault())
                .Where(node => node != null)
                .Where(node => !this._client.VerifyIfNodeIdIsValid(node.StartNodeId.Format())))
            {
                //Mark node as Processed/Completed.
                this._transmittedItems[node.StartNodeId] = (node, MonitorItemStatus.Completed);

                //Remove from Subscription
                this._client.UnsubscribeMonitorItems(new List<MonitoredItem> { node });

                //Increase the number if items to add in the next iteration
                if (this.TotalItemsToAddInCurrentIteration < 10)
                {
                    this.TotalItemsToAddInCurrentIteration++;
                }
            }
        }

        private async Task MonitorTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (LockObject)
                {
                    this.TotalItemsToAddInCurrentIteration += this.Monitor();
                }

                DisplayIterationInfo();
                await Task.Delay(this._appSettings.SubscriptionPublishIntervalMs, cancellationToken);

                if (this.IsCompleted)
                {
                    this._cancellationTokenSource.Cancel();
                }
            }
        }

        private void DisplayIterationInfo()
        {
            var totalProcessed = this._transmittedItems.Count(i => i.Value.Status == MonitorItemStatus.Completed);
            SysLog.Info("==============================================================================================");
            SysLog.Info($"Number of items added into subscription in the last iteration: {this._monitoredItemsAddedLastIterationInSubscription.Count}.");
            SysLog.Info($"Number of items removed from subscription in the last iteration: {this._itemsToBeUnsubscribed.Count}.");
            SysLog.Info($"Number of items processed: {totalProcessed} of a total of {this._transmittedItems.Count}");
            SysLog.Info($"Elapsed time: {DisplayElapseTime(this._processorElapsedTime.Elapsed)}.");
            SysLog.Info($"Waiting to add new items into the subscription in the next iteration at: {this._nextIterationTimeToAddItemsIntoSubscription}.");
            SysLog.Info("==============================================================================================\n\n");
        }

        private static string DisplayElapseTime(TimeSpan elapsedTimeSpan)
        {

            string resultElapsedTime;

            if (elapsedTimeSpan.Days > 0)
            {
                //show days
                resultElapsedTime = $"{elapsedTimeSpan.Days:D2}d:{elapsedTimeSpan.Hours:D2}h:{elapsedTimeSpan.Minutes:D2}m:{elapsedTimeSpan.Seconds:D2}s";
            }
            else if (elapsedTimeSpan.Hours > 0)
            {
                //show hours
                resultElapsedTime = $"{elapsedTimeSpan.Hours:D2}h:{elapsedTimeSpan.Minutes:D2}m:{elapsedTimeSpan.Seconds:D2}s";
            }
            else if (elapsedTimeSpan.Minutes > 0)
            {
                //show minutes
                resultElapsedTime = $"{elapsedTimeSpan.Minutes:D2}m:{elapsedTimeSpan.Seconds:D2}s";
            }
            else
            {
                //show seconds
                resultElapsedTime = $"{elapsedTimeSpan.Seconds:D2}s";
            }

            return resultElapsedTime;
        }

        private bool IsCompleted => this._transmittedItems.All(i => i.Value.Status == MonitorItemStatus.Completed);

        private void Notify(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            if (item != null)
            {
                this._queue.TryAdd(item.StartNodeId, item);
            }
        }

        private int Monitor()
        {
            var currentItemsInQueue = this._queue.Count;
            var messageToWriteInResultFile = string.Empty;

            foreach (var monitoredItem in this._queue)
            {
                if (this._transmittedItems.ContainsKey(monitoredItem.Key) &&
                    this._transmittedItems[monitoredItem.Key].Status == MonitorItemStatus.Pending) //not processed yet
                {
                    foreach (var dequeuedValue in monitoredItem.Value.DequeueValues())
                    {
                        if (StatusCode.IsGood(dequeuedValue.StatusCode))
                        {
                            messageToWriteInResultFile += this._messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItem.Value.DisplayName, dequeuedValue.Value, dequeuedValue.StatusCode.ToString(), dequeuedValue.SourceTimestamp);

                            this._transmittedItems[monitoredItem.Key] = (monitoredItem.Value, MonitorItemStatus.Completed); //marked as Processed.
                            this._itemsToBeUnsubscribed.Add(monitoredItem.Value);
                        }
                        else
                        {
                            this._messageBuilder.LogFailureMessage(monitoredItem.Value.DisplayName, dequeuedValue.StatusCode.ToString());
                        }
                    }
                }

                this._queue.TryRemove(monitoredItem.Key, out _);

                if (this._itemsToBeUnsubscribed.Count == currentItemsInQueue)
                {
                    break;
                }
            }

            if (!string.IsNullOrEmpty(messageToWriteInResultFile))
            {
                FileManager.WriteToFile(messageToWriteInResultFile, this._fileName, this._appSettings.ResultFolderPath, this._messageBuilder.GetHeaderForFile());
            }

            this._client.UnsubscribeMonitorItems(this._itemsToBeUnsubscribed);

            var numberOfItemsToSubscribe = SetTopNumberOfItemsToSubscribe(this._itemsToBeUnsubscribed);

            return numberOfItemsToSubscribe;
        }

        private int SetTopNumberOfItemsToSubscribe(IReadOnlyCollection<MonitoredItem> itemsUnsubscribed)
        {
            var numberOfToSubscribe = this._maxMonitoredItemsBatchSize
                                    - this._monitoredItemsAddedLastIterationInSubscription.Count
                                    + itemsUnsubscribed.Count;

            if (numberOfToSubscribe > this._maxMonitoredItemsBatchSize)
            {
                numberOfToSubscribe = this._maxMonitoredItemsBatchSize;
            }

            //When we are reconnecting we need to clear the items to be subscribed
            if (numberOfToSubscribe == 0 && this._client.Reconnecting)
            {
                this.TotalItemsToAddInCurrentIteration = 0;
                numberOfToSubscribe = this._maxMonitoredItemsBatchSize;
            }

            return numberOfToSubscribe;
        }
    }
}