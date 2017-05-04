using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace SplitRecords
{
    class Program
    {
        static Dictionary<string, StreamWriter> OutputFiles = new Dictionary<string, StreamWriter>();
        enum Columns
        {
            IP_ADDRESS = 0,
            RDTSC_START = 1,
            RDTSC_END = 2,
            NTP_TIME = 3,
            RTT_DELAY = 4,
            CONFIG_NAME = 5,
            RESOLVED_NAME = 7
        }

        static void ProcessFile(string FileName)
        {
            StreamReader records = new StreamReader(File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read));
            while (!records.EndOfStream)
            {
                string line = records.ReadLine();
                if (line.Contains("START"))
                {
                    continue;
                }
                string[] Values = line.Split(',');
                string name;
                string fileName;
                if (line.Contains("localhost"))
                {
                    fileName = "localhost";
                    name = "localhost";
                }
                else
                {
                    fileName = Values[(int)Columns.CONFIG_NAME] + "-" + Values[(int)Columns.IP_ADDRESS] + "-" + Values[(int)Columns.RESOLVED_NAME];
                    name = Values[(int)Columns.IP_ADDRESS];
                }
                if (!OutputFiles.ContainsKey(name))
                {
                    StreamWriter output = new StreamWriter(File.Open(fileName + ".out", FileMode.Create));
                    OutputFiles.Add(name, output);
                }
                if (name == "localhost")
                {
                    OutputFiles[name].WriteLine(line);
                }
                else
                {
                    OutputFiles[name].Write(Values[(int)Columns.IP_ADDRESS]);
                    OutputFiles[name].Write(", ");
                    OutputFiles[name].Write(Values[(int)Columns.RDTSC_START]);
                    OutputFiles[name].Write(", ");
                    OutputFiles[name].Write(Values[(int)Columns.RDTSC_END]);
                    OutputFiles[name].Write(", ");
                    OutputFiles[name].Write(Values[(int)Columns.NTP_TIME]);
                    OutputFiles[name].Write(", ");
                    OutputFiles[name].Write(Values[(int)Columns.RTT_DELAY]);
                    OutputFiles[name].WriteLine();
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: FirstFile LastFile");
                return;
            }
            string First = args[0].Substring(args[0].LastIndexOf('\\') + 1).ToUpperInvariant();
            string Last = args[1].Substring(args[1].LastIndexOf('\\') + 1).ToUpperInvariant();
            string Folder = args[0].Substring(0, args[0].LastIndexOf('\\'));
            if (Folder.Length == 0)
            {
                Folder = ".";
            }
            foreach (string s in Directory.EnumerateFiles(Folder))
            {
                string fileName = s.Substring(s.LastIndexOf('\\') + 1).ToUpperInvariant();
                if (fileName.CompareTo(First) < 0)
                {
                    continue;
                }
                if (fileName.CompareTo(Last) > 0)
                {
                    break;
                }

                ProcessFile(s);
            }
            foreach (var o in OutputFiles.Values)
            {
                o.Flush();
                o.Close();
            }
        }
    }
}
