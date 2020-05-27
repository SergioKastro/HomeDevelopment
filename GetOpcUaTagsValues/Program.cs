using log4net;
using System;
using System.ServiceProcess;

namespace GetOpcUaTagsValues
{
    static class Program
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(Program));


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new GetOpcUaTagValuesService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
