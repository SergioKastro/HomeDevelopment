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

            var opcUaClient = new OpcUaClient("opc.tcp://KPC22014549.kongsberg.master.int:53530/OPCUA/SimulationServer", appSettings);

            opcUaClient.SubscribedMonitoredItems(new List<string> { "ns=3;s=Prosys.Int1" });

            Thread.Sleep(30000);

            opcUaClient.Dispose();
        }        
    }
}
