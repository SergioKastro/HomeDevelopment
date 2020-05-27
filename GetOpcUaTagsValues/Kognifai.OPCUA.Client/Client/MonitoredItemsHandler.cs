using Kognifai.File;
using Kognifai.OPCUA.Client.Configuration;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kognifai.OPCUA.Client.Client
{
    public class MonitoredItemsHandler
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(MonitoredItemsHandler));
        private readonly OpcUaClientSession _session;
        private readonly string _fileName;
        private readonly string _directoryPath;
        private readonly string _header;

        public MonitoredItemsHandler(OpcUaClientSession session, AppSettings _appSettings)
        {
            _session = session;
            _fileName = _appSettings.PrefixFileName + string.Format("{0:yyyy_MM_dd_HH_mm_ss}", DateTime.Now) + ".csv";
            _directoryPath = _appSettings.DataFolder;
            _header = CreateHeaderFile();
        }


        public List<MonitoredItem> CreateListMonitoredItems(List<string> listNodeIds)
        {
            BrowsePathResultCollection browseResults;
            try
            {
                browseResults = _session.GetBrowseResults(listNodeIds);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to translate item paths to ids", ex);
            }

            return PopulateListMonitoredItemsFromBrowsePathResultList(listNodeIds, browseResults);
        }

        private List<MonitoredItem> PopulateListMonitoredItemsFromBrowsePathResultList(IReadOnlyList<string> sensors, BrowsePathResultCollection browseResults)
        {
            var items = new List<MonitoredItem>();

            foreach (var sensor in sensors)
            {
                try
                {
                    var browseRes = GetBrowsePathResult(browseResults, sensor);

                    if (StatusCode.IsNotGood(browseRes.StatusCode))
                    {
                        SysLog.Warn($"Failed to locate item at location {sensor}. Result for StatusCode: {browseRes.StatusCode}");
                        continue;
                    }
                    var nodeId = (NodeId)browseRes.Targets.FirstOrDefault()?.TargetId;

                    var item = new MonitoredItem
                    {
                        Handle = sensor,
                        StartNodeId = nodeId,
                        DisplayName = sensor,
                        NodeClass = NodeClass.Variable,
                        AttributeId = Attributes.Value,
                        SamplingInterval = 5000,
                        MonitoringMode = MonitoringMode.Reporting,
                        //For Polling and for MatrikonServer (Shell) we will use the default queue size, which is 0 (and it means that we will get just the latest value)
                        QueueSize = 0,
                        Filter = CreateMonitoredItemFilter()

                    };

                    item.Notification += Item_Notification;

                    items.Add(item);
                }
                catch (Exception ex)
                {
                    SysLog.Warn($"Failed to retrieve node id for item {sensor}", ex);
                }
            }


            return items;
        }

        private BrowsePathResult GetBrowsePathResult(BrowsePathResultCollection browseResults, string nodeId)
        {
            var target = new BrowsePathTarget
            {
                TargetId = nodeId
            };

            var browseRes = browseResults.Find(x => (NodeId)x.Targets.FirstOrDefault()?.TargetId == (NodeId)target.TargetId) ??
                            new BrowsePathResult { StatusCode = StatusCodes.Bad };

            return browseRes;
        }

        private void Item_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            string message;

            foreach (var value in monitoredItem.DequeueValues())
            {
                if (StatusCode.IsBad(value.StatusCode))
                {
                    message = CreateFailureMessageToWriteInResultFile(monitoredItem.DisplayName, value);
                    SysLog.Error($"Bad status code: {value.StatusCode} from server for this nodeId: {monitoredItem.DisplayName}. Please check OPC Server status. ");
                }
                else
                {
                    message = CreateSuccessMessageToWriteInResultFile(monitoredItem.DisplayName, value);
                    SysLog.Info($"Values received in publish mode: {monitoredItem.DisplayName}: { value.Value}, {value.SourceTimestamp}, {value.StatusCode}");
                }

                FileManager.WriteToFile(message, _fileName, _directoryPath, _header);
            }
        }

        private string CreateSuccessMessageToWriteInResultFile(string monitoredItemName, DataValue value)
        {
            return $"{monitoredItemName}  ,\t  { value.Value} ,\t {value.StatusCode} ,\t {value.SourceTimestamp} ,\t . ";
        }

        private string CreateFailureMessageToWriteInResultFile(string monitoredItemName, DataValue value)
        {
            return $"{monitoredItemName}  ,\t  ,\t {value.StatusCode} ,\t {value.SourceTimestamp} ,\t Bad status code: {value.StatusCode} from server for this nodeId: {monitoredItemName}. Please check OPC Server status.";
        }

        private static string CreateHeaderFile()
        {
            string header = "Tagid  ,\t Value ,\t StatusCode ,\t Timestamp ,\t Error messages";

            return header;
        }

        private MonitoringFilter CreateMonitoredItemFilter()
        {
            return new DataChangeFilter
            {
                Trigger = DataChangeTrigger.StatusValue,
                DeadbandType = (uint)DeadbandType.None
            };
        }

    }
}
