using System;
using Kognifai.OPCUA.Connector.Configuration;
using Microsoft.Extensions.Configuration;
using Topshelf;

namespace Kognifai.WindowsService.TopShelf
{
    public class Program
    {

        [STAThread]
        public static void Main()
        {
            var appSettings = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", true, true)
                .Build()
                .Get<AppSettings>();

            var topShelfExitCode = HostFactory.Run(hostConfigurator =>
            {
                hostConfigurator.SetServiceName("Kognifai OPCUA GetTags Service");
                hostConfigurator.SetDisplayName("Kognifai OPCUA GetTags Service");
                hostConfigurator.SetDescription("OPCUA Windows Service which gets the value from the static tags. ");

                hostConfigurator.StartAutomaticallyDelayed(); // Automatic (Delayed) -- only available on .NET 4.0 or later

                hostConfigurator.EnableShutdown();

                hostConfigurator.RunAsLocalSystem();

                hostConfigurator.Service<OpcUaProcessorTopShelfWrapper>(serviceConfigurator =>
                {
                    serviceConfigurator.ConstructUsing(name => new OpcUaProcessorTopShelfWrapper(appSettings));
                    serviceConfigurator.WhenStarted((s, hostControl) => s.Start(hostControl));
                    serviceConfigurator.WhenStopped((s, hostControl) => s.Stop(hostControl));
                    serviceConfigurator.WhenShutdown((s, hostControl) => s.Shutdown(hostControl));
                });
            });

            var exitCode = (int)Convert.ChangeType(topShelfExitCode, topShelfExitCode.GetTypeCode());
            Environment.ExitCode = exitCode;
        }
    }
}
