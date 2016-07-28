using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TimeCalibration.MedianFilter
{
    class Program
    {
        static double GetMedianValue(List<double> Values)
        {
            List<double> valuesArr = Values.ToList();
            valuesArr.Sort();
            return valuesArr[valuesArr.Count / 2];
        }
        static void Main(string[] args)
        {
            List<double> medianValues = new List<double>();
            string line;
            int col;
            int depth;
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: MedianFilter columnnumber depth");
                return;
            }
            col = int.Parse(args[0]) - 1;
            depth = int.Parse(args[1]);
            for (;;)
            {
                double value;
                string[] columns;
                line = Console.ReadLine();
                if (line == null)
                {
                    break;
                }
                columns = line.Split(',');
                value = double.Parse(columns[col]);
                medianValues.Add(value);
                while (medianValues.Count > depth)
                {
                    medianValues.RemoveAt(0);
                }
                if (medianValues.Count == depth)
                {
                    double median = GetMedianValue(medianValues);
                    Console.WriteLine(line + "," + median);
                }
            }
        }
    }
}
