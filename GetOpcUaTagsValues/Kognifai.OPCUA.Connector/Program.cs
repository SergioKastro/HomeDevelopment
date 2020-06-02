using System;
using Kognifai.OPCUA.Connector.Configuration;
using log4net;
using Microsoft.Extensions.Configuration;

namespace Kognifai.OPCUA.Connector
{
    public static class Program
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(Program));

        [STAThread]
        public static void Main(string[] args)
        {
            SysLog.Info("Starting app.");

            var appSettings = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", true, true)
                .Build()
                .Get<AppSettings>();

            var processor = new OpcUaProcessor(appSettings);
            processor.Start();
            processor.Stop();
        }
    }
}
