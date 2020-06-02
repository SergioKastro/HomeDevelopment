using log4net;
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Timers;
using Kognifai.OPCUA.Connector.Interfaces;

namespace GetOpcUaTagsValues
{
    public partial class GetOpcUaTagValuesService : ServiceBase
    {
        private readonly IOpcUaProcessor _opcUaProcessor;
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(GetOpcUaTagValuesService));

        private readonly Timer _timer;
        private DateTime _scheduleTime;

        public GetOpcUaTagValuesService(IOpcUaProcessor opcUaProcessor)
        {
            InitializeComponent();
            _opcUaProcessor = opcUaProcessor;

            _timer = new Timer();
            _scheduleTime = DateTime.Today.AddDays(1).AddHours(12).AddMinutes(5); // Schedule to run once a day at 7:00 a.m.
            
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                SysLog.Info("OPCUA GetTags windows service starting ...");

                // Update the service state to Start Pending.
                var serviceStatus = new ServiceStatus
                {
                    dwCurrentState = ServiceState.SERVICE_START_PENDING,
                    dwWaitHint = 100000
                };
                SetServiceStatus(ServiceHandle, ref serviceStatus);

                //Here is the main task for my Service: We will call here our OPCUA Client
                _timer.Enabled = true;
                _timer.Interval = _scheduleTime.Subtract(DateTime.Now).TotalSeconds * 1000;
                _timer.Elapsed += OnElapsedTime;
                _timer.Start();
                

                // Update the service state to Running.
                serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
                SetServiceStatus(ServiceHandle, ref serviceStatus);


                SysLog.Info("OPCUA GetTags windows service started.");
            }
            catch (Exception ex)
            {
                SysLog.Error("Failed to start OPCUA GetTags windows service.", ex);
                ExitCode = 1;
                Stop();
            }
        }

        protected override void OnStop()
        {
            try
            {
                SysLog.Info("OPCUA GetTags windows service stopping ...");
                // Update the service state to Stop Pending.
                var serviceStatus = new ServiceStatus
                {
                    dwCurrentState = ServiceState.SERVICE_STOP_PENDING,
                    dwWaitHint = 100000
                };
                SetServiceStatus(ServiceHandle, ref serviceStatus);

                //Stop the task
                _timer.Enabled = false;
                _opcUaProcessor.Stop();


                // Update the service state to Stopped.
                serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
                SetServiceStatus(ServiceHandle, ref serviceStatus);
                SysLog.Info("OPCUA GetTags windows service stopped.");
            }
            catch (Exception ex)
            {
                SysLog.Error("Failed to stop OPCUA GetTags windows service.", ex);
                this.ExitCode = 1;
            }

        }

        protected override void OnShutdown()
        {
            OnStop();
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            // 1. Process Schedule Task
            _opcUaProcessor.Start();


            // 2. If tick for the first time, reset next run to every 24 hours
            if (_timer.Interval != 24 * 60 * 60 * 1000)
            {
                _timer.Interval = 24 * 60 * 60 * 1000;
            }
        }


        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };


        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);
    }
}
