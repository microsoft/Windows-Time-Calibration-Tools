using System;
using System.Collections.Generic;
using System.Threading;
using System.DirectoryServices.ActiveDirectory;

namespace Microsoft.TimeCalibration.Ntp
{
    class NtpSurvey
    {
        struct Server
        {
            public string Name;
            public NtpInitiator Ntp;
            public uint Count;
        };
        /// <summary>
        /// </summary>
        /// <param name="args">
        /// args[0] - Delay
        /// args[1] - Count
        /// args[2] - Stats 
        /// args[3] - Domain name
        /// </param>
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: Delay Count [Stats] [Domain]");
                return;
            }

            List<Server> servers = new List<Server>();
            List<string> serverNames = new List<string>();
            ManualResetEvent wait = new ManualResetEvent(false);

            uint count = uint.Parse(args[1]);
            uint delay = uint.Parse(args[0]);
            string domainName = null;
            bool stats = false;

            int serversRemaining = 0;
            int totalServerCount = 0;
            int responsesReceived = 0;

            if (args.Length >= 3 && args[2] == "1")
            {
                stats = true;
            }

            if (args.Length >= 4)
            {
                domainName = args[3];

                try
                {
                    DirectoryContext context = new DirectoryContext(DirectoryContextType.Domain, domainName);
                    Domain domain = Domain.GetDomain(context);
                    foreach (DomainController dc in domain.FindAllDiscoverableDomainControllers())
                    {
                        serverNames.Add(dc.Name);
                    }
                }
                catch (ActiveDirectoryObjectNotFoundException)
                {
                    Console.Error.WriteLine("Domain \"" + domainName + "\" not found");
                    return;
                }
            }
            else {
                for (;;)
                {
                    string s = Console.ReadLine();
                    if (s == null)
                    {
                        break;
                    }
                    serverNames.Add(s);
                }

            }

            foreach (string serverName in serverNames)
            {
                try
                {
                    Server server = new Server();
                    server.Name = serverName;
                    server.Count = count;
                    server.Ntp = new NtpInitiator(serverName, delay);
                    server.Ntp.SetSampleReady(delegate (Sample s)
                    {
                        server.Count--;
                        int localResponse = Interlocked.Increment(ref responsesReceived);
                        if (server.Count == 0)
                        {
                            server.Ntp.SetSampleReady(null);
                            server.Ntp.Stop();
                            Console.Error.WriteLine("Server: " + server.Name + " completed");
                            if (Interlocked.Decrement(ref serversRemaining) == 0)
                            {
                                wait.Set();
                            }
                        }
                    });
                    Console.Error.WriteLine("Adding server: " + server.Name);
                    servers.Add(server);
                    serversRemaining++;
                    totalServerCount++;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    Console.Error.WriteLine("Can't resolve server:" + serverName);
                }
            }

            foreach (Server server in servers)
            {
                server.Ntp.Start();
            }

            Console.Error.WriteLine("Waiting for all servers to compelte");
            for (long i = 0; i < count + 2; i++)
            {
                long percentComplete = (responsesReceived * 100) / (totalServerCount * count);
                Console.Error.WriteLine(percentComplete + "% of responses received");

                wait.WaitOne((int)(delay));
            }
            Console.WriteLine("Server,OffsetMedian,OffsetDeviation,RttMedian,RttDeviation");

            foreach (Server server in servers)
            {
                double offsetMedian;
                double offsetDeviation;
                double roundTripMedain;
                double roundTripDeviation;

                if (server.Ntp.ComputeSampleStats(out offsetMedian, out offsetDeviation, out roundTripMedain, out roundTripDeviation))
                {
                    Console.WriteLine(server.Name + "," + offsetMedian + "," + offsetDeviation + "," + roundTripMedain + "," + roundTripDeviation);
                }
                else {
                    Console.WriteLine(server.Name + ",No Response,No Response,No Response,No Response");
                }
            }

            if (stats)
            {
                Console.WriteLine();
                Console.Write("Leap,");
                Console.Write("Version,");
                Console.Write("Mode,");
                Console.Write("Stratum,");
                Console.Write("PollInteval,");
                Console.Write("Precision,");
                Console.Write("PollInterval,");
                Console.Write("RootDelay,");
                Console.Write("RootDispersion,");
                Console.Write("RefId");
                Console.WriteLine();
                foreach (Server server in servers)
                {
                    uint leap;
                    uint version;
                    uint mode;
                    uint stratum;
                    double precision;
                    double pollInterval;
                    ulong rootDelay;
                    ulong rootDispersion;
                    string refId;

                    if (server.Ntp.GetServerMetadata(
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
                        Console.Write(server.Name + ",");
                        Console.Write(leap + ",");
                        Console.Write(version + ",");
                        Console.Write(mode + ",");
                        Console.Write(stratum + ",");
                        Console.Write(precision + ",");
                        Console.Write(pollInterval + ",");
                        Console.Write(rootDelay + ",");
                        Console.Write(rootDispersion + ",");
                        Console.Write(refId);
                        Console.WriteLine();
                    }
                }

            }
        }
    }
}
