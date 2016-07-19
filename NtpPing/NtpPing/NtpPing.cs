using System;
using System.Threading;

namespace Microsoft.TimeCalibration.Ntp
{
    class NtpPing
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
                uint warmUp = 10;
                NtpInitiator ntp = new NtpInitiator(args[0], period);

                Console.WriteLine("Pinging Server: " + args[0]);
                ntp.SetSampleReady(delegate (Sample s)
                {
                    if (warmUp > 0)
                    {
                        warmUp--;
                        return;
                    }
                    Console.WriteLine("Received response - Offset = " + (double)s.Offset / NtpInitiator.OneHundredNsInOneSecond + " RTT = " + (double)s.RoundTripTime / NtpInitiator.OneHundredNsInOneSecond);
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
                double offsetMedian;
                double offsetDeviation;
                double roundTripMedain;
                double roundTripDeviation;
                uint leap;
                uint version;
                uint mode;
                uint stratum;
                double precision;
                double pollInterval;
                ulong rootDelay;
                ulong rootDispersion;
                string refId;
                if (ntp.ComputeSampleStats(out offsetMedian, out offsetDeviation, out roundTripMedain, out roundTripDeviation) &&
                    ntp.GetServerMetadata(
                    out leap,
                    out version,
                    out mode,
                    out stratum,
                    out pollInterval,
                    out precision,
                    out rootDelay,
                    out rootDispersion,
                    out refId))
                {
                    Console.WriteLine("Server responded");
                    Console.WriteLine("OffsetMedian: " + offsetMedian);
                    Console.WriteLine("OffsetDeviation: " + offsetDeviation);
                    Console.WriteLine("RoundTripMedain: " + roundTripMedain);
                    Console.WriteLine("RoundTripDeviation: " + roundTripDeviation);
                    Console.WriteLine("Leap:" + leap);
                    Console.WriteLine("Version:" + version);
                    Console.WriteLine("Mode:" + mode);
                    Console.WriteLine("Stratum:" + stratum);
                    Console.WriteLine("PollInterval:" + pollInterval);
                    Console.WriteLine("Precision:" + precision);
                    Console.WriteLine("RootDelay:" + rootDelay / NtpInitiator.OneHundredNsInOneSecond);
                    Console.WriteLine("RootDispersion:" + rootDispersion / NtpInitiator.OneHundredNsInOneSecond);
                    Console.WriteLine("RefId:" + refId);
                }
                else {
                    Console.WriteLine("No response");
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                Console.Error.WriteLine("Can't reach server:" + args[0]);
            }
        }
    }
}
