using Kognifai.File;
using Kognifai.OPCUA.Client.Client;
using Kognifai.OPCUA.Client.Configuration;
using log4net;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Kognifai.OPCUA.Client
{
    public class Program
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(Program));

        [STAThread]
        public static void Main(string[] args)
        {
            SysLog.Info("Staring app.");

            var appSettings = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", true, true)
                .Build()
                .Get<AppSettings>();

            //Get list sensors
            var listSensors = FileManager.DataReading(appSettings.SensorListFilePath);

            var opcUaClient = new OpcUaClient(appSettings);

            opcUaClient.SubscribedMonitoredItems(listSensors);

            Thread.Sleep(60000);

            opcUaClient.Dispose();
        }
    }
}
