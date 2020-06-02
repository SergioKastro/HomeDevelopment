﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private static readonly ILog Logging = LogManager.GetLogger(typeof(OpcUaProcessor));
        private static readonly object LockObject = new object();

        private readonly AppSettings _appSettings;
        private readonly IOpcUaClient _client;
        private readonly ConcurrentDictionary<NodeId, MonitoredItem> _queue = new ConcurrentDictionary<NodeId, MonitoredItem>();
        private readonly Dictionary<NodeId, (MonitoredItem Item, MonitorItemStatus Status)> _transmittedItems = new Dictionary<NodeId, (MonitoredItem Item, MonitorItemStatus Status)>();
        private readonly TimeSpan _monitoredItemsBatchIntervalMs;
        private readonly int _maxMonitoredItemsBatchSize;

        private string _fileName;
        private bool _started;
        private int _totalItemsToAddInCurrentIteration = 0;
        private CancellationTokenSource _cancellationTokenSource;

        private enum MonitorItemStatus
        {
            Pending,
            Completed
        }

        public OpcUaProcessor(AppSettings appSettings)
        {
            this._appSettings = appSettings;
            this._client = new OpcUaClient(appSettings, this.Notify);
            this._maxMonitoredItemsBatchSize = appSettings.MonitoredItemsBatchSize;
            this._monitoredItemsBatchIntervalMs = TimeSpan.FromMilliseconds(appSettings.MonitoredItemsBatchIntervalMs);
        }

        public void Start()
        {
            if (!this._started)
            {
                this._fileName = this._appSettings.PrefixFileName + $"{DateTime.Now:yyyy_MM_dd_HH_mm_ss}" + ".csv";
                this._cancellationTokenSource = new CancellationTokenSource();
                var listSensors = FileManager.DataReading(this._appSettings.SensorListFilePath);

                lock (LockObject)
                {
                    if (_client != null)
                    {
                        var items = _client.CreateMonitoredItems(listSensors);

                        foreach (var transmittedItem in items)
                        {

                            this._transmittedItems[transmittedItem.StartNodeId] = (transmittedItem, MonitorItemStatus.Pending);
                        }
                    }
                }

                this._started = true;

                // initial items to add.
                lock (LockObject)
                {
                    this._totalItemsToAddInCurrentIteration = this._maxMonitoredItemsBatchSize;
                }


                Task.WhenAny(
                        this.AddItemsToSubscriptionTask(this._cancellationTokenSource.Token),
                        this.ProcessTask(this._cancellationTokenSource.Token))
                    .Wait();

                //When completed we stop the whole process
                this.Stop();
                this._started = false;
            }
        }

        public void Stop()
        {
            if (this._started)
            {
                var values = GetCurrentMonitoredItemsFromConcurrentQueue();

                //Remove monitoredItems from the Subscription
                this._client.UnsubscribeMonitorItems(values, this.Notify);

                //Close the subscription and the client session
                this._client.Dispose();

                //Cancel all the tasks
                this._cancellationTokenSource.Cancel();

                Logging.Info($"Next execution time: {DateTime.Now.AddMinutes(_appSettings.ServiceIntervalMinutes)}.");
            }
        }

        public void Shutdown()
        {
            this.Stop();
        }

        private List<MonitoredItem> GetCurrentMonitoredItemsFromConcurrentQueue()
        {
            var keys = _queue.Keys.ToList();
            var values = new List<MonitoredItem>();
            foreach (var key in keys)
            {
                if (_queue.TryRemove(key, out var metric))
                {
                    values.Add(metric);
                }
            }

            return values;
        }

        private async Task ProcessTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (LockObject)
                {
                    this._totalItemsToAddInCurrentIteration += this.Monitor();
                }

                LogNumberOfItemsProcessed();

                await Task.Delay(_appSettings.SubscriptionPublishIntervalMs, cancellationToken);

                if (this.IsCompleted)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
        }

        private void LogNumberOfItemsProcessed()
        {
            var totalProcessed = this._transmittedItems.Count(i => i.Value.Status == MonitorItemStatus.Completed);
            Logging.Info($"Total Items Processed: {totalProcessed}.");
            Logging.Info("=========================================");
        }

        private async Task AddItemsToSubscriptionTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (LockObject)
                {
                    var batchMonitoredItemsToAddInSubscription = this._transmittedItems
                        .Where(i => i.Value.Status == MonitorItemStatus.Pending)
                        .Select(i => i.Value.Item)
                        .Take(this._totalItemsToAddInCurrentIteration)
                        .ToList();

                    this._client.SubscribedMonitoredItems(batchMonitoredItemsToAddInSubscription, this.Notify);

                    Logging.Debug($"Subscribe: {batchMonitoredItemsToAddInSubscription.Count} items.");

                    this._totalItemsToAddInCurrentIteration = 0;
                }

                //We delay here to have control how many items we are adding per interval:
                //_totalItemsToAddPerIteration per _monitoredItemsBatchIntervalMs (100 items per minute)
                await Task.Delay(this._monitoredItemsBatchIntervalMs, cancellationToken);

                if (this.IsCompleted)
                {
                    this._cancellationTokenSource.Cancel();
                }
            }
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
            var itemsToBeUnsubscribed = new List<MonitoredItem>();
            var currentItemsInQueue = this._queue.Count;
            var messageToWriteInResultFile = string.Empty;

            foreach (var monitoredItem in _queue)
            {
                if (this._transmittedItems.ContainsKey(monitoredItem.Key) &&
                    this._transmittedItems[monitoredItem.Key].Status == MonitorItemStatus.Pending) //not processed yet
                {
                    foreach (var dequeuedValue in monitoredItem.Value.DequeueValues())
                    {
                        if (StatusCode.IsGood(dequeuedValue.StatusCode))
                        {
                            messageToWriteInResultFile += CreateSuccessMessageToWriteInResultFile(monitoredItem.Value.DisplayName, dequeuedValue);

                            this._transmittedItems[monitoredItem.Key] = (monitoredItem.Value, MonitorItemStatus.Completed); //marked as Processed.
                            itemsToBeUnsubscribed.Add(monitoredItem.Value);
                        }
                        else
                        {
                            LogFailureMessage(monitoredItem.Value.DisplayName, dequeuedValue);
                        }
                    }
                }

                this._queue.TryRemove(monitoredItem.Key, out _);

                if (itemsToBeUnsubscribed.Count == currentItemsInQueue)
                {
                    break;
                }
            }

            if (!string.IsNullOrEmpty(messageToWriteInResultFile))
            {
                FileManager.WriteToFile(messageToWriteInResultFile, _fileName, _appSettings.DataFolder, FileManager.GetHeaderForFile());
            }

            this._client.UnsubscribeMonitorItems(itemsToBeUnsubscribed, this.Notify);
            Logging.Info($"Unsubscribed: {itemsToBeUnsubscribed.Count} items.");


            var numberOfItemsToSubscribe = SetTopNumberOfItemsToSubscribe(itemsToBeUnsubscribed);

            return numberOfItemsToSubscribe;
        }

        private int SetTopNumberOfItemsToSubscribe(IReadOnlyCollection<MonitoredItem> itemsUnsubscribed)
        {
            var numberOfToSubscribe = itemsUnsubscribed.Count;

            if (numberOfToSubscribe > this._maxMonitoredItemsBatchSize)
            {
                numberOfToSubscribe = this._maxMonitoredItemsBatchSize;
            }

            if (numberOfToSubscribe == 0 && _client.Reconnected)
            {
                this._totalItemsToAddInCurrentIteration = 0;
                numberOfToSubscribe = this._maxMonitoredItemsBatchSize;
            }

            return numberOfToSubscribe;
        }

        private static string CreateSuccessMessageToWriteInResultFile(string monitoredItemName, DataValue value)
        {
            var message = $"{monitoredItemName}  ,\t  { value.Value} ,\t {value.StatusCode} ,\t {value.SourceTimestamp} . \n ";

            Logging.Debug($"Read value for: {message}");

            return message;
        }

        private static void LogFailureMessage(string monitoredItemName, DataValue value)
        {
            Logging.Error(
                $"{monitoredItemName}  ,\t  {value.StatusCode} ,\t {value.SourceTimestamp} ,\t Bad status code: {value.StatusCode} from server for this nodeId: {monitoredItemName}. Please check OPC Server status.");

        }
    }
}