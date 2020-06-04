using System;
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
        private IOpcUaClient _client;
        private readonly ConcurrentDictionary<NodeId, MonitoredItem> _queue = new ConcurrentDictionary<NodeId, MonitoredItem>();
        private readonly Dictionary<NodeId, (MonitoredItem Item, MonitorItemStatus Status)> _transmittedItems = new Dictionary<NodeId, (MonitoredItem Item, MonitorItemStatus Status)>();
        private readonly TimeSpan _monitoredItemsBatchIntervalMs;
        private readonly int _maxMonitoredItemsBatchSize;

        private string _fileName;
        private bool _started;
        private int _totalItemsToAddInCurrentIteration;
        public int TotalItemsToAddInCurrentIteration
        {
            get => _totalItemsToAddInCurrentIteration;
            set => _totalItemsToAddInCurrentIteration = value > this._maxMonitoredItemsBatchSize ? 
                this._maxMonitoredItemsBatchSize : 
                value;
        }
        private CancellationTokenSource _cancellationTokenSource;
        private List<MonitoredItem> _monitoredItemsAddedLastIterationInSubscription;
        private DateTime _nextIterationTimeToAddItemsIntoSubscription;
        private List<MonitoredItem> _itemsToBeUnsubscribed;

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
        }

        public void Start()
        {
            if (!this._started)
            {
                try
                {
                    Logging.Info("Starting OpcUa Processor ....");

                    this._client = new OpcUaClient(_appSettings, this.Notify);

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

                        // initial items to add
                        this.TotalItemsToAddInCurrentIteration = this._maxMonitoredItemsBatchSize;
                    }

                    this._started = true;

                    Task.WhenAny(
                            this.AddItemsToSubscriptionTask(this._cancellationTokenSource.Token),
                            this.ProcessTask(this._cancellationTokenSource.Token))
                        .Wait();

                    //When completed we stop the whole process
                    Logging.Info("OpcUa Processor completed. Stopping and setting next interval.");

                }
                catch (Exception ex)
                {
                    Logging.Error("OpcUa Processor unexpected error. Stopping and setting next interval.", ex);
                }
                finally
                {
                    this.Stop();
                    this._started = false;
                }
            }
        }

        public void Stop()
        {
            this._client.StopTimerCheckConnection();

            if (this._started)
            {
                Logging.Info("Stopping OpcUa processor ....");

                //Remove monitoredItems from the Subscription
                var values = GetCurrentMonitoredItemsFromConcurrentQueue();
                this._client.UnsubscribeMonitorItems(values, this.Notify);

                //Close the subscription and the client session
                this._client.Dispose();

                //Cancel all the tasks
                this._cancellationTokenSource.Cancel();
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
                    this.TotalItemsToAddInCurrentIteration += this.Monitor();
                }

                await Task.Delay(_appSettings.SubscriptionPublishIntervalMs, cancellationToken);
                DisplayIterationInfo();

                if (this.IsCompleted)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
        }

        private void DisplayIterationInfo()
        {
            var totalProcessed = this._transmittedItems.Count(i => i.Value.Status == MonitorItemStatus.Completed);
            Logging.Info("==============================================================================");
            Logging.Info($"Number of items added into subscription in the last iteration: {this._monitoredItemsAddedLastIterationInSubscription.Count}.");
            Logging.Info($"Number of items removed from subscription in the last iteration: {this._itemsToBeUnsubscribed.Count}.");
            Logging.Info($"Items in the queue waiting to read their values: {this._queue.Count}.");
            Logging.Info($"Total Items Processed: {totalProcessed}.");
            Logging.Info($"Total Items to add in next iteration: {this.TotalItemsToAddInCurrentIteration}.");
            Logging.Info($"Waiting for the next iteration at: {this._nextIterationTimeToAddItemsIntoSubscription}.");
            Logging.Info("==============================================================================");
        }

        private async Task AddItemsToSubscriptionTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (LockObject)
                {
                    _monitoredItemsAddedLastIterationInSubscription = this._transmittedItems
                        .Where(i => i.Value.Status == MonitorItemStatus.Pending)
                        .Select(i => i.Value.Item)
                        .Take(this.TotalItemsToAddInCurrentIteration)
                        .ToList();

                    this._client.SubscribedMonitoredItems(_monitoredItemsAddedLastIterationInSubscription, this.Notify);

                    this.TotalItemsToAddInCurrentIteration = 0;
                }

                //We delay here to have control how many items we are adding per interval:
                //_totalItemsToAddPerIteration per _monitoredItemsBatchIntervalMs (100 items per minute)
                _nextIterationTimeToAddItemsIntoSubscription = DateTime.Now.AddMilliseconds(this._monitoredItemsBatchIntervalMs.TotalMilliseconds);
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
            _itemsToBeUnsubscribed = new List<MonitoredItem>();
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
                            _itemsToBeUnsubscribed.Add(monitoredItem.Value);
                        }
                        else
                        {
                            LogFailureMessage(monitoredItem.Value.DisplayName, dequeuedValue);
                        }
                    }
                }

                this._queue.TryRemove(monitoredItem.Key, out _);

                if (_itemsToBeUnsubscribed.Count == currentItemsInQueue)
                {
                    break;
                }
            }

            if (!string.IsNullOrEmpty(messageToWriteInResultFile))
            {
                FileManager.WriteToFile(messageToWriteInResultFile, _fileName, _appSettings.DataFolder, FileManager.GetHeaderForFile());
            }

            this._client.UnsubscribeMonitorItems(_itemsToBeUnsubscribed, this.Notify);

            var numberOfItemsToSubscribe = SetTopNumberOfItemsToSubscribe(_itemsToBeUnsubscribed);

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
                this.TotalItemsToAddInCurrentIteration = 0;
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