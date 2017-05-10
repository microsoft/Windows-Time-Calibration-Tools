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
        private struct NtpServer
        {
            public string ConfiguredName;
            public IPAddress Address;
            public string ResolvedName;
        }
        private struct Sampler
        {
            public Process Child;
            public NtpServer Server;
            public ProcessStartInfo StartInfo;
        }
        private StreamWriter Output;
        private static string BaseFileName;
        private static string LogFilePath;
        private static int Hour;
        private static System.Threading.ManualResetEvent Shutdown;
        private static int ChildCount;
        private Dictionary<NtpServer, Task> RunningTasks = new Dictionary<NtpServer, Task>();
        private System.Threading.Timer ConfigRefresh;
        private object writeLock = new Object();
        private string ConfiguredServiceName;
        private EventLog Log;

        public Monitoring(string [] Argv)
        {
            InitializeComponent();
            if (Argv.Length > 0)
            {
                ConfiguredServiceName = Argv[0];
            }
            else
            {
                ConfiguredServiceName = ServiceName;
            }
        }

        protected void UpdateServerList()
        {
            string keyName = "SYSTEM\\CurrentControlSet\\Services\\" + ConfiguredServiceName + "\\Servers";
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyName);
            if (key == null)
            {
                EventLog.WriteEntry("Missing configuration subkey:" + keyName);
                Stop();
                return;
            }
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
                    if (interval == null || interval.Length == 0) 
                    {
                        interval = "5000";
                    }

                    Log.WriteEntry("Monitoring NTP server: " + server.ConfiguredName + " IPAddress: " + server.Address.ToString());

                    Sampler sample = new Sampler();
                    sample.Server = server;


                    if (server.ConfiguredName == "localhost")
                    {
                        sample.StartInfo = new ProcessStartInfo("OsTimeSampler.exe", "1000 3600");
                    }
                    else
                    {
                        sample.StartInfo = new ProcessStartInfo("NtpSampler.exe", server.Address.ToString() + " " + interval + " 3600");
                    }

                    sample.StartInfo.CreateNoWindow = true;
                    sample.StartInfo.RedirectStandardOutput = true;
                    sample.StartInfo.UseShellExecute = false;
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
            Guid instanceId = Guid.NewGuid();
            string keyName = "SYSTEM\\CurrentControlSet\\Services\\" + ConfiguredServiceName + "\\Config";
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyName);
            if (key == null)
            {
                EventLog.WriteEntry("Missing configuration subkey: " + keyName);
                Stop();
                return;
            }
            if (EventLog.SourceExists(ConfiguredServiceName) && EventLog.Exists(ConfiguredServiceName))
            {
                Log = new EventLog(ConfiguredServiceName);
                Log.Source = ConfiguredServiceName;
            }
            else
            {
                Log = EventLog;
            }

            ChildCount = 0;
            Output = null;
            Hour = -1;

            if (key.GetValue("BasePath") == null)
            {
                EventLog.WriteEntry("Missing configuration value: BasePath");
                Stop();
                return;
            }
            else
            {
                BaseFileName = key.GetValue("BasePath").ToString() + "\\" + instanceId.ToString() + ".";
            }

            if (key.GetValue("LogPath") != null)
            {
                LogFilePath = key.GetValue("LogPath").ToString() + "\\" + instanceId.ToString() + ".";
            }
            else
            {
                LogFilePath = null;
            }
            Shutdown = new System.Threading.ManualResetEvent(false);

            ConfigRefresh = new System.Threading.Timer((object o) => { UpdateServerList(); });
            ConfigRefresh.Change(0, 60000);
        }

        protected override void OnStop()
        {
            ConfigRefresh.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            Shutdown.Set();

            Task[] tasks;
            lock (RunningTasks)
            {
                tasks = new Task[RunningTasks.Values.Count];
                RunningTasks.Values.CopyTo(tasks, 0);
            }
            

            foreach (Task task in tasks)
            {
                try
                {
                    task.Wait();
                }
                catch (System.AggregateException)
                {

                }
            }
            if (Output != null)
            {
                Output.Flush();
                Output.Close();
            }
        }

        private async Task ReadSampler(Sampler sampler)
        {
            for (;;)
            {
                if (Shutdown.WaitOne(0))
                {
                    sampler.Child.Kill();
                    System.Threading.Interlocked.Decrement(ref ChildCount);
                    break;
                }
                if (sampler.Child.HasExited)
                {
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
                WriteSample(sampler.Server.Address.ToString(), s, sampler.Server.ConfiguredName + "," + sampler.Server.Address.ToString() + "," + sampler.Server.ResolvedName);
            }
        }

        private void WriteSample(string Prefix, string Data, string Suffix)
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
                    Log.WriteEntry("Writing to file: " + fileName);
                    Output = File.CreateText(fileName);
                }
                Output.WriteLine(Prefix + "," + Data + "," + Suffix);
                Output.Flush();
            }
        }

        private Dictionary<IPAddress, string> ForwardDnsResolve(string[] DnsNames)
        {
            Dictionary<IPAddress, string> names = new Dictionary<IPAddress, string>();
            Dictionary<string, Task<IPHostEntry>> resolvers = new Dictionary<string, Task<IPHostEntry>>();
            DateTime now = DateTime.Now;
            StreamWriter resolverLog = null;
            
            if (LogFilePath != null)
            {
                string fileName = LogFilePath + now.Year.ToString("D4") + now.Month.ToString("D2") + now.Day.ToString("D2") + now.Hour.ToString("D2") + ".resolver.csv";
                resolverLog = new StreamWriter(File.Open(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            }
            if (resolverLog != null) resolverLog.WriteLine("Starting name resolution at " + DateTime.Now.ToString());
            foreach (string dnsName in DnsNames)
            {
                IPAddress ip;
                if (IPAddress.TryParse(dnsName, out ip))
                {
                    names.Add(ip, dnsName);
                }
                else
                {
                    resolvers.Add(dnsName, Dns.GetHostEntryAsync(dnsName));
                }
            }

            foreach (var entry in resolvers)
            {
                if (resolverLog != null) resolverLog.Write(entry.Key + ",");
                try
                {
                    entry.Value.Wait();
                    foreach (var ip in entry.Value.Result.AddressList)
                    {
                        if (resolverLog != null) resolverLog.Write(ip.ToString() + ",");
                        if (!names.ContainsKey(ip))
                        {
                            names.Add(ip, entry.Key);
                        }
                    }
                }
                catch (System.AggregateException ex)
                {
                    if (resolverLog != null) resolverLog.Write("FAILED ");
                    if (ex.InnerException is System.Net.Sockets.SocketException)
                    {
                        System.Net.Sockets.SocketException s = (System.Net.Sockets.SocketException)ex.InnerException;
                        if (resolverLog != null) resolverLog.Write(s.ErrorCode + "," + ex.InnerException.Message);
                    }
                    else
                    {
                        if (resolverLog != null) resolverLog.Write(ex.InnerException.Message.ToString());
                    }
                }
                if (resolverLog != null) resolverLog.WriteLine();
            }
            if (resolverLog != null)
            {
                resolverLog.WriteLine("Ending name resolution at " + DateTime.Now.ToString());
                resolverLog.Flush();
                resolverLog.Close();
                resolverLog.Dispose();
            }
            return names;
        }

        private Dictionary<IPAddress, string> ReverseDnsResolve(IPAddress[] IpAddresses)
        {
            Dictionary<IPAddress, string> names = new Dictionary<IPAddress, string>();
            Dictionary<IPAddress, Task<IPHostEntry>> resolvers = new Dictionary<IPAddress, Task<IPHostEntry>>();
            foreach (var ip in IpAddresses)
            {
                resolvers.Add(ip, Dns.GetHostEntryAsync(ip));
            }

            foreach (var entry in resolvers)
            {
                try
                {
                    entry.Value.Wait();
                    names.Add(entry.Key, entry.Value.Result.HostName);
                }
                catch (System.AggregateException)
                {
                }
            }
            return names;
        }

        private NtpServer[] ResolveServerNames(string [] DnsNames)
        {
            List<NtpServer> servers = new List<NtpServer>();
            Dictionary<IPAddress, string> addresses = ForwardDnsResolve(DnsNames);
            Dictionary<IPAddress, string> hostnames = ReverseDnsResolve(addresses.Keys.ToArray());

            foreach (var address in addresses)
            {
                NtpServer server = new NtpServer();
                server.ConfiguredName = address.Value;
                server.ResolvedName = (hostnames.ContainsKey(address.Key) ? hostnames[address.Key] : address.Value);
                server.Address = address.Key;
                servers.Add(server);

            }
            return servers.ToArray();
        }
    }
}
