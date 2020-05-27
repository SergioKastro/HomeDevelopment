using Kognifai.File;
using log4net;
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Timers;

namespace GetOpcUaTagsValues
{
    public partial class GetOpcUaTagValuesService : ServiceBase
    {
        readonly Timer timer = new Timer();
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(GetOpcUaTagValuesService));
        private readonly string fileName = "Kognifai.TagsData.ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".csv";

        public GetOpcUaTagValuesService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_START_PENDING,
                dwWaitHint = 100000
            };
            SetServiceStatus(ServiceHandle, ref serviceStatus);

            //Here is the main task for my Service: We will call here our OPCUA Client

            FileManager.WriteToFile("Service is started at " + DateTime.Now, fileName);
            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            timer.Interval = 5000; //number in miliseconds  
            timer.Enabled = true;

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(ServiceHandle, ref serviceStatus);
        }

        protected override void OnStop()
        {
            // Update the service state to Stop Pending.
            ServiceStatus serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_STOP_PENDING,
                dwWaitHint = 100000
            };
            SetServiceStatus(ServiceHandle, ref serviceStatus);

            FileManager.WriteToFile("Service is stopped at " + DateTime.Now, fileName);


            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(ServiceHandle, ref serviceStatus);
        }

        protected override void OnShutdown()
        {
            OnStop();
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            FileManager.WriteToFile("Service is recall at " + DateTime.Now, fileName);
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
