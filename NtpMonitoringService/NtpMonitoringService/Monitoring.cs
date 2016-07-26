using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.IO;

namespace NtpMonitoringService
{
    public partial class Monitoring : ServiceBase
    {
        struct Sampler
        {
            public Process Child;
            public string Name;
            public ProcessStartInfo StartInfo;
        }
        StreamWriter Output;
        static string BaseFileName;
        static int Hour;
        static System.Threading.ManualResetEvent Shutdown;
        static int ChildCount;
        Dictionary<string, Task> RunningTasks = new Dictionary<string, Task>();
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
            string[] servers = key.GetValueNames();
            List<string> removedServers = new List<string>();
            List<string> addedServers = new List<string>();
            lock (RunningTasks)
            {
                foreach (string server in servers)
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


                foreach (string server in RunningTasks.Keys)
                {
                    if (servers.Contains(server))
                    {
                        continue;
                    }
                    removedServers.Add(server);
                }

                foreach (string server in addedServers)
                {
                    Sampler sample = new Sampler();
                    sample.Name = server;

                    if (sample.Name == "localhost")
                    {
                        sample.StartInfo = new ProcessStartInfo("TimeSampler.exe", "1000 3600");
                    }
                    else
                    {
                        sample.StartInfo = new ProcessStartInfo("NtpSampler.exe", sample.Name + " 1000 3600");
                    }

                    sample.StartInfo.CreateNoWindow = true;
                    sample.StartInfo.RedirectStandardOutput = true;
                    sample.StartInfo.UseShellExecute = false;
                    EventLog.WriteEntry("Monitoring NTP server: " + sample.Name);
                    sample.Child = Process.Start(sample.StartInfo);
                    System.Threading.Interlocked.Increment(ref ChildCount);
                    RunningTasks.Add(server, ReadSampler(sample));
                }
                foreach (string server in removedServers)
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
                    EventLog.WriteEntry("Stopping monitor for NTP server: " + sampler.Name);
                    sampler.Child.Kill();
                    System.Threading.Interlocked.Decrement(ref ChildCount);
                    break;
                }
                if (sampler.Child.HasExited)
                {
                    EventLog.WriteEntry("Restarting monitor for NTP server: " + sampler.Name);
                    lock (RunningTasks)
                    {
                        if (!RunningTasks.ContainsKey(sampler.Name))
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
                WriteSample(sampler.Name, s);
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
    }
}
