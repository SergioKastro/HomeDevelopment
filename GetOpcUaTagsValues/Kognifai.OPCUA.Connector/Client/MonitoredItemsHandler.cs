using System;
using System.Collections.Generic;
using System.Linq;
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


        public MonitoredItemsHandler(OpcUaClientSession session, AppSettings appSettings)
        {
            _session = session;
            _appSettings = appSettings;
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
                        SamplingInterval = _appSettings.SamplingIntervalMs,
                        MonitoringMode = MonitoringMode.Reporting,
                        //For Polling and for MatrikonServer (Shell) we will use the default queue size, which is 0 (and it means that we will get just the latest value)
                        QueueSize = 0,
                        Filter = CreateMonitoredItemFilter()

                    };

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
