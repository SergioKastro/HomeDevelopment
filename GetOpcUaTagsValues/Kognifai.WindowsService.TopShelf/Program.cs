using log4net;
using System;
using Kognifai.OPCUA.Connector.Configuration;
using Microsoft.Extensions.Configuration;
using Topshelf;

namespace Kognifai.WindowsService.TopShelf
{
    public class Program
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(Program));

        public static void Main()
        {
            SysLog.Info("Starting app.");

            var appSettings = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", true, true)
                .Build()
                .Get<AppSettings>();

            var rc = HostFactory.Run(host =>
            {
                host.SetServiceName("Kognifai OPCUA GetTags Service");
                host.SetDisplayName("Kognifai OPCUA GetTags Service");
                host.SetDescription("OPCUA Windows Service which gets the value from the static tags. ");

                host.StartAutomaticallyDelayed(); // Automatic (Delayed) -- only available on .NET 4.0 or later

                host.RunAsLocalSystem();

                host.Service<OpcUaProcessorTopShelfWrapper>(sc =>
                {
                    sc.ConstructUsing(name => new OpcUaProcessorTopShelfWrapper(appSettings));
                    sc.WhenStarted((s, hostControl) => s.Start(hostControl));
                    sc.WhenStopped((s, hostControl) => s.Stop(hostControl));
                    sc.WhenShutdown((s, hostControl) => s.Shutdown(hostControl));
                });
            });

            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            Environment.ExitCode = exitCode;
        }
    }
}
