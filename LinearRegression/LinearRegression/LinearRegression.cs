using System;
using System.Collections.Generic;
using System.Numerics;

namespace Microsoft.TimeCalibration.LinearRegression
{
    /// <summary>
    /// Caclulate the co-efficients of a function y = ß*x + α that is the closest
    /// match to the reference points.
    /// </summary>
    class LinearRegression
    {
        struct Point
        {
            public BigInteger x;
            public BigInteger y;
        };

        /// <summary>
        /// Calculate α and ß 
        /// ß = (n * ΣXY - ΣX * ΣY) / (n * ΣXX - ΣX^2)
        /// α = (ΣY / n) - ß * (ΣX / n);
        /// </summary>
        /// <param name="DataPoints"></param>
        static void CalcCoEfficients(List<Point> DataPoints, out double α, out double ß)
        {
            // Both A0 and A1 can be decomposed into a set of sigma operations.
            // Calculate the common Σ values
            BigInteger ΣX = 0;
            BigInteger ΣY = 0;
            BigInteger ΣXY = 0;
            BigInteger ΣXX = 0;
            BigInteger ΣYY = 0;
            long n = DataPoints.Count;

            foreach (Point p in DataPoints)
            {
                ΣX += p.x;
                ΣY += p.y;
                ΣXY += p.x * p.y;
                ΣXX += p.x * p.x;
                ΣYY += p.y * p.y;
            }

            ß = (double)(n * ΣXY - ΣX * ΣY) / (double)(n * ΣXX - ΣX * ΣX);
            α = (double)(ΣY / n) - ß * (double)(ΣX / n);
        }

        static double LinearFunction(double x, double α, double ß)
        {
            return x * ß + α; ;
        }

        static double CalcRmsFromLinear(List<Point> DataPoints, double α, double ß)
        {
            double accumulator = 0;
            foreach (Point p in DataPoints)
            {
                double delta = (double)p.y - LinearFunction((double)p.x, α, ß);
                accumulator += delta * delta;
            }
            return Math.Sqrt(accumulator);
        }

        /// <summary>
        /// Read in a CSV file
        /// </summary>
        /// <param name="args">
        /// args[0] - X column
        /// args[1] - Y column
        /// </param>
        static void Main(string[] args)
        {
            List<Point> DataPoints = new List<Point>();
            List<Point> FilteredDataPoints = new List<Point>();
            double α;
            double ß;
            double maxDeviation;
            double rms;
            string line;
            string header;
            string[] columnNames;
            int xColumn = -1;
            int yColumn = -1;
            int n;

            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: LinearRegression columnID_X columnID_Y MaxDeviation < data.csv");
                return;
            }
            maxDeviation = double.Parse(args[2]);

            header = Console.ReadLine();
            columnNames = header.Split(',');

            for (int i = 0; i < columnNames.Length; i++)
            {
                if (columnNames[i].ToUpperInvariant().Contains(args[0].ToUpperInvariant()))
                {
                    xColumn = i;
                }
                if (columnNames[i].ToUpperInvariant().Contains(args[1].ToUpperInvariant()))
                {
                    yColumn = i;
                }
            }

            if (xColumn == -1)
            {
                Console.Error.WriteLine("Can't find column named " + args[0]);
            }
            if (yColumn == -1)
            {
                Console.Error.WriteLine("Can't find column named " + args[1]);
            }
            if (xColumn == -1 || yColumn == -1)
            {
                return;
            }

            for (;;)
            {
                string[] values;
                Point p = new Point();
                line = Console.ReadLine();
                if (null == line)
                {
                    break;
                }

                values = line.Split(',');
                if (!BigInteger.TryParse(values[xColumn], out p.x))
                {
                    continue;
                }
                if (!BigInteger.TryParse(values[yColumn], out p.y))
                {
                    continue;
                }

                DataPoints.Add(p);
            }
            CalcCoEfficients(DataPoints, out α, out ß);
            rms = CalcRmsFromLinear(DataPoints, α, ß);
            n = DataPoints.Count;
            Console.WriteLine("Data set fitted to f(x)= ßx + α where:");
            Console.WriteLine("α=" + α.ToString() + " ß=" + ß.ToString() + " RMS=" + rms);

            foreach (Point p in DataPoints)
            {
                double delta = (double)p.y - LinearFunction((double)p.x, α, ß);
                if (Math.Abs(delta) < maxDeviation)
                {
                    FilteredDataPoints.Add(p);
                }
            }
            Console.WriteLine("After excluding points that are " + maxDeviation + " from linear (removed " + (DataPoints.Count - FilteredDataPoints.Count) + " points):");
            CalcCoEfficients(FilteredDataPoints, out α, out ß);
            rms = CalcRmsFromLinear(FilteredDataPoints, α, ß);
            Console.WriteLine("Data set fitted to f(x)= ßx + α where:");
            Console.WriteLine("α=" + α.ToString() + " ß=" + ß.ToString() + " RMS=" + rms);

        }
    }
}
