using System;
using System.Collections.Generic;
using System.Linq;
using Kognifai.File;
using Kognifai.OPCUA.Connector.Configuration;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector.Client
{
    public class MonitoredItemsHandler
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(MonitoredItemsHandler));
        private readonly OpcUaClientSession _session;
        private readonly AppSettings _appSettings;
        private readonly string _notLocatedFileName;

        public MonitoredItemsHandler(OpcUaClientSession session, AppSettings appSettings)
        {
            _session = session;
            _appSettings = appSettings;
            _notLocatedFileName = _appSettings.PrefixNoLocatedSensorsFileName + $"{DateTime.Now:yyyy_MM_dd_HH_mm_ss}" + ".csv";
        }


        public List<MonitoredItem> CreateListMonitoredItems(List<string> listNodeIds)
        {
            var items = new List<MonitoredItem>();

            foreach (var sensorId in listNodeIds.Distinct().ToList())
            {
                try
                {
                    var item = new MonitoredItem
                    {
                        Handle = sensorId,
                        StartNodeId = new NodeId(sensorId),
                        DisplayName = sensorId,
                        NodeClass = NodeClass.Variable,
                        AttributeId = Attributes.Value,
                        SamplingInterval = _appSettings.SamplingIntervalMs,
                        MonitoringMode = MonitoringMode.Reporting,
                        // For MatrikonServer (Shell) we will use the default queue size, which is 0 (and it means that we will get just the latest value)
                        QueueSize = 0,
                        Filter = CreateMonitoredItemFilter()

                    };

                    items.Add(item);
                }
                catch (Exception ex)
                {
                    var messageToWriteInResultFile = $"Failed to create monitoredItem for item {sensorId}. Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    SysLog.Warn(messageToWriteInResultFile, ex);
                    FileManager.WriteToFile(messageToWriteInResultFile, _notLocatedFileName, _appSettings.ResultFolderPath);
                }
            }
            
            return items;
        }

        public bool VerifyIfNodeIdIsValid(string sensorId)
        {
            if (!_session.IsConnected)
            {
                return false;
            }

            var browseRes = _session.GetBrowseResultForOneNode(sensorId);

            if (browseRes.StatusCode != StatusCodes.BadNodeIdInvalid && browseRes.StatusCode != StatusCodes.BadNodeIdUnknown)
            {
                return true;
            }

            //We know for sure the NodeId cannot be found in the OPC Server because it has reported that.
            //So we mark as not Valid
            var messageToWriteInResultFile =
                $"Failed to locate item at location {sensorId}. Result for StatusCode: {browseRes.StatusCode}. Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            SysLog.Warn(messageToWriteInResultFile);
            FileManager.WriteToFile(messageToWriteInResultFile, _notLocatedFileName, _appSettings.ResultFolderPath);

            return false;
        }

        private static MonitoringFilter CreateMonitoredItemFilter()
        {
            return new DataChangeFilter
            {
                Trigger = DataChangeTrigger.StatusValue,
                DeadbandType = (uint)DeadbandType.None
            };
        }

    }
}
