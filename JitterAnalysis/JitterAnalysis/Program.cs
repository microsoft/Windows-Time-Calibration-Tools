using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace JitterAnalysis
{
    class Program
    {
        struct Sample
        {
            public BigInteger start;
            public BigInteger end;
            public BigInteger remote;
        };
        /// <summary>
        /// Calculate α and ß 
        /// ß = (n * ΣXY - ΣX * ΣY) / (n * ΣXX - ΣX^2)
        /// α = (ΣY / n) - ß * (ΣX / n);
        /// </summary>
        /// <param name="DataPoints"></param>
        static void CalcCoEfficients(List<Sample> Samples, out double α, out double ß)
        {
            // Both A0 and A1 can be decomposed into a set of sigma operations.
            // Calculate the common Σ values
            BigInteger ΣX = 0;
            BigInteger ΣY = 0;
            BigInteger ΣXY = 0;
            BigInteger ΣXX = 0;
            BigInteger ΣYY = 0;
            long n = Samples.Count;

            foreach (Sample p in Samples)
            {
                BigInteger x = p.end / 2 + p.start / 2;
                BigInteger y = p.remote;

                ΣX += x;
                ΣY += y;
                ΣXY += x * y;
                ΣXX += x * x;
                ΣYY += y * y;
            }

            ß = (double)(n * ΣXY - ΣX * ΣY) / (double)(n * ΣXX - ΣX * ΣX);
            α = (double)(ΣY / n) - ß * (double)(ΣX / n);
        }

        static double LinearFunction(double x, double α, double ß)
        {
            return x * ß + α; ;
        }
        static double CalcStdDev(List<double> DataPoints)
        {
            double mean = 0;
            double STDEV = 0;
            foreach (double p in DataPoints)
            {
                mean += p;
            }
            mean /= DataPoints.Count;

            foreach (double p in DataPoints)
            {
                STDEV += (mean - p) * (mean-p);
            }

            STDEV /= DataPoints.Count;
            return Math.Sqrt(STDEV);
        }

        static void Main(string[] args)
        {
            List<Sample> Samples = new List<Sample>();
            List<Sample> Window = new List<Sample>();
            List<double> InboundDelta = new List<double>();
            List<double> OutboundDelta = new List<double>();
            List<double> Rtt = new List<double>();
            double α;
            double ß;
            string line;
            int WindowDepth = 20;
            bool Verbose = false;

            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: JitterAnalysis WindowDepth [Verbose] < data.csv");
                return;
            }
            WindowDepth = int.Parse(args[0]);
            if (args.Length == 2 && args[1].CompareTo("Verbose") == 0)
            {
                Verbose = true;
            }
            for (;;)
            {
                string[] values;
                Sample s = new Sample();
                line = Console.ReadLine();
                if (null == line)
                {
                    break;
                }

                values = line.Split(',');
                if (!BigInteger.TryParse(values[0], out s.start))
                {
                    continue;
                }
                if (!BigInteger.TryParse(values[1], out s.end))
                {
                    continue;
                }
                if (!BigInteger.TryParse(values[2], out s.remote))
                {
                    continue;
                }
                Samples.Add(s);
            }
            // First couple of samples after startup tend to be the noisiest
            Samples.RemoveRange(0, 20);
            foreach (Sample s in Samples)
            {
                while (Window.Count > WindowDepth)
                {
                    Window.RemoveAt(0);

                }
                Window.Add(s);
                if (Window.Count < WindowDepth)
                {
                    continue;
                }
                CalcCoEfficients(Window, out α, out ß);
                double startTime = LinearFunction((double)Window[WindowDepth / 2].start, α, ß);
                double endTime = LinearFunction((double)Window[WindowDepth / 2].end, α, ß); ;
                double remoteTime = (double)Window[WindowDepth / 2].remote;
                InboundDelta.Add(remoteTime - startTime);
                OutboundDelta.Add(endTime  - remoteTime);
                Rtt.Add(endTime - startTime);
                if (Verbose)
                {
                    Console.Write(remoteTime - startTime);
                    Console.Write(",");
                    Console.Write(endTime - remoteTime);
                    Console.Write(",");
                    Console.Write(endTime - startTime);
                    Console.WriteLine();
                }
                if (Rtt.Count + WindowDepth > Samples.Count)
                {
                    break;
                }
            }
            if (!Verbose)
            {
                Console.WriteLine("Transmit STDEV:\t" + (int)(CalcStdDev(InboundDelta) / 10) + "us");
                Console.WriteLine("Recieve STDEV:\t" + (int)(CalcStdDev(OutboundDelta) / 10) + "us");
                Console.WriteLine("RTT STDEV:\t" + (int)(CalcStdDev(Rtt) / 10) + "us");
            }

        }
    }
}