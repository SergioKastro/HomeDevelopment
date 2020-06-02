using log4net;
using System;
using System.ServiceProcess;
using Kognifai.OPCUA.Connector;
using Kognifai.OPCUA.Connector.Configuration;
using Microsoft.Extensions.Configuration;


namespace GetOpcUaTagsValues
{
    public static class Program
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(Program));


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var appSettings = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", true, true)
                .Build()
                .Get<AppSettings>();

            if (!Environment.UserInteractive)
            {
                // Startup as service.
                var servicesToRun = new ServiceBase[]
                {
                    new GetOpcUaTagValuesService(new OpcUaProcessor(appSettings))
                };
                ServiceBase.Run(servicesToRun);

            }
            else
            {
                // Startup as application
                var processor = new OpcUaProcessor(appSettings);
                processor.Start();
                processor.Stop();
            }
        }
    }
}
