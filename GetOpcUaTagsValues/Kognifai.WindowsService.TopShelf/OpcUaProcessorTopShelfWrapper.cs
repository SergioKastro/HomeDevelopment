using System;
using System.Timers;
using Kognifai.OPCUA.Connector;
using Kognifai.OPCUA.Connector.Configuration;
using log4net;
using Topshelf;

namespace Kognifai.WindowsService.TopShelf
{
    public class OpcUaProcessorTopShelfWrapper : ServiceControl
    {
        private readonly AppSettings _appSettings;
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaProcessorTopShelfWrapper));


        private readonly Timer _timer;
        private readonly OpcUaProcessor _processor;

        public OpcUaProcessorTopShelfWrapper(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _processor = new OpcUaProcessor(_appSettings);

            _timer = new Timer
            {
                AutoReset = true,
                Enabled = true
            };

            _timer.Elapsed += OnTimerOnElapsed;
        }

        private void OnTimerOnElapsed(object sender, ElapsedEventArgs eventArgs)
        {
            SysLog.Info("Starting the OPCUA Processor.");
            _timer.Stop();

            _processor.Start();

            //Set the correct Timer interval after the first time runs
            _timer.Interval = TimeSpan.FromMinutes(_appSettings.ServiceIntervalMinutes).TotalMilliseconds; 
            _timer.Start();
        }


        public bool Start(HostControl hostControl)
        {
            _timer.Start();

            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _timer.Stop();

            _processor.Stop();

            return false;
        }

        public void Shutdown(HostControl hostControl)
        {
            _timer.Stop();

            _processor.Shutdown();
        }
    }
}