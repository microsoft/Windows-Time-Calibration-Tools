using System.ServiceProcess;

namespace NtpMonitoringService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Monitoring()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
