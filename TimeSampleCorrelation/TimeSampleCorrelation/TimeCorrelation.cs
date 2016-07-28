/*++

Copyright (c) Microsoft Corporation

Module Name:

    TimeSampleCalibration

Abstract:
    
    This module computes the phase offset between two sets of time samples, keyed on TSC

Author:

    Alan Jowett (alanjo) 19-March-2016

--*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;


namespace Microsoft.TimeCalibration.TimeSampleCorrelation
{
    class TimeCorrelation
    {
        struct Sample
        {
            public long tsc;
            public long tscStart;
            public long tscEnd;
            public long timeStamp;
        };

        struct Point
        {
            public double x;
            public double y;
        };

        /// <summary>
        /// Given a collections of points on a function, compute the interpolated value at x
        /// </summary>
        /// <param name="Sample">Sample set</param>
        /// <param name="x">x value on the interpolated graph</param>
        /// <returns>y value at x</returns>
        static double Interpolate(Point[] Sample, double x)
        {
            // Calculate the interpolation using a Lagrange polynomial
            double y = 0;

            for (int i = 0; i < Sample.Length; i++)
            {
                double c = 1;
                for (int j = 0; j < Sample.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    c *= (x - Sample[j].x) / (Sample[i].x - Sample[j].x);
                }
                y += Sample[i].y * c;
            }

            return y;
        }

        /// <summary>
        /// Read a collection of samples from the source file. 
        /// Samples are assumed to be in the format:
        /// start_tsc, end_tsc, os time
        /// </summary>
        /// <param name="FileName">File containing samples</param>
        /// <param name="delta">TSC delta to apply</param>
        /// <returns>List of samples</returns>
        static List<Sample> ReadSamples(string FileName, long delta, int StartingColumn)
        {
            StreamReader timeStamps = new StreamReader(File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read));
            List<Sample> samples = new List<Sample>();
            while (!timeStamps.EndOfStream)
            {
                Sample sample = new Sample();
                long tscStart;
                long tscEnd;
                string[] fields = timeStamps.ReadLine().Split(',');
                if (fields.Length < 3)
                {
                    continue;
                }
                if (!long.TryParse(fields[0 + StartingColumn], out tscStart))
                {
                    continue;
                }
                if (!long.TryParse(fields[1 + StartingColumn], out tscEnd))
                {
                    continue;
                }
                if (!long.TryParse(fields[2 + StartingColumn], out sample.timeStamp))
                {
                    continue;
                }

                // Apply delta
                tscStart -= delta;
                tscEnd -= delta;

                // Average start and end
                sample.tsc = (tscEnd + tscStart) / 2;
                sample.tscStart = tscStart;
                sample.tscEnd = tscEnd;

                samples.Add(sample);
            }
            timeStamps.Close();
            return samples;
        }

        static bool ValidatePoints(Point[] Points)
        {
            List<double> delta = new List<double>();
            double previous = Points[0].x;
            double mean = 0;
            double rms = 0;
            for (int i = 1; i < Points.Length; i ++)
            {
                delta.Add(Points[i].x - previous);
                previous = Points[i].x;
            }

            delta.ForEach((double x) => { mean += x; });
            mean /= delta.Count;
            delta.ForEach((double x) => { rms += (x - mean) * (x - mean); });
            rms /= delta.Count;
            rms = Math.Sqrt(rms);

            return mean > rms;
        }

        /// <summary>
        /// Search for the nearest guest sample for this TSC
        /// </summary>
        /// <param name="Tsc"></param>
        /// <param name="Samples"></param>
        /// <param name="Points"></param>
        /// <returns></returns>
        static bool FindSamples(long Tsc, Sample[] Samples, ref Point[] Points)
        {
            int low = 0;
            int high = Samples.Length;

            if (Tsc < Samples[0].tsc)
            {
                return false;
            }

            if (Tsc > Samples[high - 1].tsc)
            {
                return false;
            }

            while (high - low > 1)
            {
                int mid = (high + low) / 2;
                if (Tsc > Samples[mid].tsc)
                {
                    low = mid;
                }
                else {
                    high = mid;
                }
            }
            if (low < 2)
            {
                return false;
            }
            if (Samples.Length < (high + 2))
            {
                return false;
            }
            Points[0].x = Samples[low - 2].tsc;
            Points[0].y = Samples[low - 2].timeStamp;
            Points[1].x = Samples[low - 1].tsc;
            Points[1].y = Samples[low - 1].timeStamp;
            Points[2].x = Samples[low].tsc;
            Points[2].y = Samples[low].timeStamp;
            Points[3].x = Samples[low + 1].tsc;
            Points[3].y = Samples[low + 1].timeStamp;
            Points[4].x = Samples[low + 2].tsc;
            Points[4].y = Samples[low + 2].timeStamp;
            return true;
        }

        /// <summary>
        /// Given two time samples, compute the clock skew.
        /// </summary>
        /// <param name="args">Root sample, Guest sample, TSC offset of guest</param>
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: root.csv guest.csv TscOffset");
                return;
            }

            // TscOffest is ulong, but is often a negative delta, so for convenience
            // cast it to a long
            long detla = (long)ulong.Parse(args[2]);
            int startingColumn = 0;
            if (args.Length > 3)
            {
                startingColumn = int.Parse(args[3]);
            }

            Sample[] rootSamples = ReadSamples(args[0], 0, startingColumn).ToArray<Sample>();
            Sample[] guestSamples = ReadSamples(args[1], detla, startingColumn).ToArray<Sample>();
            Point[] interopData = new Point[5];

            if ((rootSamples.Length == 0) || (guestSamples.Length == 0))
            {
                return;
            }

            // The tsc times won't match.
            // Intropolate the guest timestamps to get the approximate value they would have had at root TSC

            // starting at sample 2, calculate the approximate guest time at rootSamples[i].
            for (int i = 0; i < rootSamples.Length; i++)
            {
                // Gather a spread of 5 samples around the point to evaluate
                double skew;
                double rtt;
                if (!FindSamples(rootSamples[i].tsc, guestSamples, ref interopData))
                {
                    continue;
                }

                if (!ValidatePoints(interopData))
                {
                    continue;
                }

                // Interpolate the guest time at the root tsc timestamp.
                skew = Interpolate(interopData, rootSamples[i].tsc) - rootSamples[i].timeStamp;
                rtt = Interpolate(interopData, rootSamples[i].tscEnd) - Interpolate(interopData, rootSamples[i].tscStart);

                // Convert time stamp to human readable form.
                DateTime date = DateTime.FromFileTime(rootSamples[i].timeStamp);

                // Print date, skew (in us units)
                Console.Write(date.ToString() + ",");
                Console.Write((Math.Round(skew) / 10).ToString() + "," + (Math.Round(rtt) / 10).ToString());
                Console.WriteLine();
            }
        }
    }
}
