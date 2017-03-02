using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.IO;
using System.Net;

namespace NtpMonitoringService
{
    public partial class Monitoring : ServiceBase
    {
        struct NtpServer
        {
            public string ConfiguredName;
            public IPAddress Address;
            public string ResolvedName;
        }
        struct Sampler
        {
            public Process Child;
            public NtpServer Server;
            public ProcessStartInfo StartInfo;
        }
        StreamWriter Output;
        static string BaseFileName;
        static int Hour;
        static System.Threading.ManualResetEvent Shutdown;
        static int ChildCount;
        Dictionary<NtpServer, Task> RunningTasks = new Dictionary<NtpServer, Task>();
        System.Threading.Timer ConfigRefresh;
        private object writeLock = new Object();

        public Monitoring()
        {
            InitializeComponent();

            string keyName = "SYSTEM\\CurrentControlSet\\Services\\" + ServiceName + "\\Config";
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyName);

            ChildCount = 0;
            Output = null;
            Hour = -1;

            BaseFileName = key.GetValue("BasePath").ToString() + "\\" + Guid.NewGuid().ToString() + ".";
            Shutdown = new System.Threading.ManualResetEvent(false);
            
        }

        protected void UpdateServerList()
        {
            string keyName = "SYSTEM\\CurrentControlSet\\Services\\" + ServiceName + "\\Servers";
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyName);
            NtpServer[] servers = ResolveServerNames(key.GetValueNames());
            List<NtpServer> removedServers = new List<NtpServer>();
            List<NtpServer> addedServers = new List<NtpServer>();

            lock (RunningTasks)
            {
                foreach (NtpServer server in servers)
                {
                    // Existing entry
                    if (RunningTasks.ContainsKey(server))
                    {
                        continue;
                    }

                    else
                    {
                        addedServers.Add(server);
                    }
                }

                foreach (NtpServer server in RunningTasks.Keys)
                {
                    if (RunningTasks.ContainsKey(server))
                    {
                        continue;
                    }
                    removedServers.Add(server);
                }

                foreach (NtpServer server in addedServers)
                {
                    string interval = (string)key.GetValue(server.ConfiguredName);

                    EventLog.WriteEntry("Monitoring NTP server: " + server.ConfiguredName + " IPAddress: " + server.Address.ToString());

                    Sampler sample = new Sampler();
                    sample.Server = server;

                    sample.StartInfo.CreateNoWindow = true;
                    sample.StartInfo.RedirectStandardOutput = true;
                    sample.StartInfo.UseShellExecute = false;

                    if (server.ConfiguredName == "localhost")
                    {
                        sample.StartInfo = new ProcessStartInfo("TimeSampler.exe", "1000 3600");
                    }
                    else
                    {
                        sample.StartInfo = new ProcessStartInfo("NtpSampler.exe", server.Address.ToString() + " " + interval + " 3600");
                    }
                    sample.Child = Process.Start(sample.StartInfo);
                    System.Threading.Interlocked.Increment(ref ChildCount);
                    RunningTasks.Add(server, ReadSampler(sample));

                }
                foreach (NtpServer server in removedServers)
                {
                    RunningTasks.Remove(server);
                }
            }
        }

        protected override void OnStart(string[] args)
        {
            ConfigRefresh = new System.Threading.Timer((object o) => { UpdateServerList(); });
            ConfigRefresh.Change(0, 60000);
        }

        protected override void OnStop()
        {
            ConfigRefresh.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            ConfigRefresh.Dispose();
            ConfigRefresh = null;

            Shutdown.Set();

            Task[] tasks;
            lock (RunningTasks)
            {
                tasks = new Task[RunningTasks.Values.Count];
                RunningTasks.Values.CopyTo(tasks, 0);
            }

            foreach (Task task in tasks)
            {
                task.Wait();
            }
            Output.Flush();
            Output.Close();
        }

        private async Task ReadSampler(Sampler sampler)
        {
            for (;;)
            {
                if (Shutdown.WaitOne(0))
                {
                    EventLog.WriteEntry("Stopping monitor for NTP server: " + sampler.Server.ResolvedName);
                    sampler.Child.Kill();
                    System.Threading.Interlocked.Decrement(ref ChildCount);
                    break;
                }
                if (sampler.Child.HasExited)
                {
                    EventLog.WriteEntry("Restarting monitor for NTP server: " + sampler.Server.ResolvedName);
                    lock (RunningTasks)
                    {
                        if (!RunningTasks.ContainsKey(sampler.Server))
                        {
                            return;
                        }
                    }
                    sampler.Child = Process.Start(sampler.StartInfo);
                    continue;
                }
                string s = await sampler.Child.StandardOutput.ReadLineAsync();
                if (s == null)
                {
                    continue;
                }

                WriteSample(sampler.Server.ConfiguredName + "," + sampler.Server.Address.ToString() + ","  + sampler.Server.ResolvedName, s);
            }
        }

        private void WriteSample(string Name, string Data)
        {
            DateTime now = DateTime.Now;
            lock (writeLock)
            {
                string fileName;
                if (Hour != now.Hour)
                {
                    Hour = now.Hour;
                    if (Output != null)
                    {
                        Output.Flush();
                        Output.Close();
                    }
                    fileName = BaseFileName + now.Year.ToString("D4") + now.Month.ToString("D2") + now.Day.ToString("D2") + now.Hour.ToString("D2") + now.Minute.ToString("D2") + ".csv";
                    EventLog.WriteEntry("Writing to file: " + fileName);
                    Output = File.CreateText(fileName);
                }
                Output.WriteLine(Name + "," + Data);
                Output.Flush();
            }
        }

        private NtpServer[] ResolveServerNames(string [] DnsNames)
        {
            List<NtpServer> servers = new List<NtpServer>();
            foreach (string dnsName in DnsNames)
            {
                // Resolve configured DNS name to list of addresses (to get list of servers)
                IPHostEntry configuredHostEntry = Dns.GetHostEntry(dnsName);
                foreach (IPAddress ip in configuredHostEntry.AddressList)
                {
                    // Resolve the address back to an actual host name
                    IPHostEntry resolveHostEntry = Dns.GetHostEntry(ip);
                    NtpServer server = new NtpServer();
                    server.ConfiguredName = dnsName;
                    server.ResolvedName = resolveHostEntry.HostName;
                    server.Address = ip;
                    servers.Add(server);
                }
            }
            return servers.ToArray();
        }
    }
}
