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
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaProcessorTopShelfWrapper));
        private readonly AppSettings _appSettings;
        private readonly OpcUaProcessor _processor;
        private const int InitialIntervalInMsc = 5000;

        private Timer _timer;

        public OpcUaProcessorTopShelfWrapper(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _processor = new OpcUaProcessor(_appSettings);
            _processor.ReconnectingEventHandler += ProcessorOnReconnecting;

            SetTimer(InitialIntervalInMsc);//Initial timer will run after 5 sec
        }

        private void ProcessorOnReconnecting(object sender, EventArgs e)
        {
            //Re-start immediately the processor when reconnecting 
            _timer.Stop();
            SetTimer(InitialIntervalInMsc);
            _timer.Start();
        }

        private void SetTimer(int intervalMs)
        {
            // Create a timer .
            _timer = new Timer(intervalMs); 

            // Hook up the Elapsed event for the timer. 
            _timer.Elapsed += OnTimerOnElapsed;
            _timer.AutoReset = true;
        }

        private void OnTimerOnElapsed(object sender, ElapsedEventArgs eventArgs)
        {
            _timer.Stop();

            if (_processor.IsRunning)
            {
                //Force stop. The processor didn't finish and we force to start again after the ServiceIntervalMinutes happens
                SysLog.Info($"Restarting the processor after {_appSettings.ServiceIntervalMinutes} minutes running.");
                _processor.Stop();
            }

            //Set the correct Timer interval after the first time runs
            _timer.Interval = TimeSpan.FromMinutes(_appSettings.ServiceIntervalMinutes).TotalMilliseconds;

            SysLog.Info("\n\n Starting OPCUA Windows Service for Static tags." +
                        $"\n Next execution time: {DateTime.Now.AddMinutes(_appSettings.ServiceIntervalMinutes)}.\n");

            _timer.Start();

            _processor.Start();
        }


        public bool Start(HostControl hostControl)
        {
            _timer.Start();

            return true;
        }

        public bool Stop(HostControl hostControl)
        {

            SysLog.Info("Stopping windows service ....");

            _timer.Enabled = false;
            _timer.Stop();

            try
            {
                _processor.Stop();
            }
            catch(Exception ex)
            {
                SysLog.Error("Could not stop the opcua processor properly. Unexpected error", ex);
                return false;
            }

            SysLog.Info("OPCUA Windows Service stopped.");
            return true;
        }

        public bool Shutdown(HostControl hostControl)
        {
            SysLog.Info("Shutting down windows service ....");

            _timer.Stop();

            try
            {
                _processor.Shutdown();
            }
            catch (Exception ex)
            {
                SysLog.Error("Could not shutdown the opcua processor properly. Unexpected error", ex);
                return false;
            }

            SysLog.Info("OPCUA Windows Service shutdown completed.");
            return true;
        }
    }
}