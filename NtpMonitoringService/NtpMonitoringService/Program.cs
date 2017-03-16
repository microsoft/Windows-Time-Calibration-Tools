using System.ServiceProcess;

namespace NtpMonitoringService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string [] Argv)
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Monitoring(Argv)
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
