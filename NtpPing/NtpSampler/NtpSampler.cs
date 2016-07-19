using System;
using System.Threading;

namespace Microsoft.TimeCalibration.Ntp
{
    class NtpSampler
    {
        /// <summary>
        /// </summary>
        /// <param name="args">
        /// args[0] - Server Name
        /// args[1] - Delay
        /// args[1] - Count
        /// </param>
        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                {
                    Console.Error.WriteLine("Usage: Server Delay Count");
                    return;
                }
                ManualResetEvent wait = new ManualResetEvent(false);
                uint count = uint.Parse(args[2]);
                uint period = uint.Parse(args[1]);
                NtpInitiator ntp = new NtpInitiator(args[0], period);

                Console.WriteLine("RDTSC_START,RDTSC_END,NTP_TIME,RTT_DELAY");
                ntp.SetSampleReady(delegate (Sample s)
                {
                    Console.WriteLine(s.RdTscStart + "," + s.RdTscEnd + "," + (s.ReceiveTimestamp / 2 + s.TransmitTimestamp / 2) + "," + s.RoundTripTime);
                    count--;
                    if (count == 0)
                    {
                        ntp.SetSampleReady(null);
                        ntp.Stop();
                        wait.Set();
                    }
                });
                ntp.Start();

                wait.WaitOne((int)((count + 1) * period));
            }
            catch (System.Net.Sockets.SocketException)
            {
                Console.Error.WriteLine("Can't reach server:" + args[0]);
            }
        }
    }
}
