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
                if (columns.Length > col)
                {
                    try
                    {
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
                    catch(Exception)
                    {
                        // In case of parsing or other exceptions, just output the original string.
                        // This helps process and retain headers and such in input the data
                        Console.WriteLine(line);
                    }
                }
                else
                {
                    // Not enough columns in the current line.Just output the original string and move on.
                    // This helps process and retain headers and such in input the data
                    Console.WriteLine(line);
                }
            }
        }
    }
}
