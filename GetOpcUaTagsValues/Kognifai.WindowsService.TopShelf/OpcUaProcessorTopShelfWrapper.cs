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

        private Timer _timer;
        private OpcUaProcessor _processor;

        public OpcUaProcessorTopShelfWrapper(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _processor = new OpcUaProcessor(_appSettings);

            SetTimer(1000);//Initial timer will run after 1 sec
        }

        private void SetTimer(int intervalMs)
        {
            // Create a timer .
            _timer = new Timer(intervalMs); 

            // Hook up the Elapsed event for the timer. 
            _timer.Elapsed += OnTimerOnElapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        private void OnTimerOnElapsed(object sender, ElapsedEventArgs eventArgs)
        {
            _timer.Enabled = false;

            if (_processor.IsRunning)
            {
                _processor.Stop();
            }

            _processor.Start();

            //Set the correct Timer interval after the first time runs
            _timer.Interval = TimeSpan.FromMinutes(_appSettings.ServiceIntervalMinutes).TotalMilliseconds;
            _timer.Enabled = true;

            SysLog.Info($"Next execution time: {DateTime.Now.AddMinutes(_appSettings.ServiceIntervalMinutes)}.");
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