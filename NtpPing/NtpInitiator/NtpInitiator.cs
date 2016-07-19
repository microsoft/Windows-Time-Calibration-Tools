using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Numerics;

namespace Microsoft.TimeCalibration.Ntp
{
    using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
    /// <summary>
    /// A NTP timestamp in the 64bit format
    /// </summary>
    public struct NtpTimeStamp
    {
        public uint Seconds;
        public uint Fraction;
    }

    /// <summary>
    /// A NTP time duration in 32bit format
    /// </summary>
    public struct NtpShortTimeInterval
    {
        public ushort Seconds;
        public ushort Fraction;
    }

    /// <summary>
    /// Collected data about an NTP exchange
    /// </summary>
    public struct Sample
    {
        public byte Leap;
        public byte Version;
        public byte Mode;
        public byte Stratum;
        public sbyte PollInterval;
        public sbyte Precision;
        public uint RootDelay;
        public uint RootDispersion;
        public uint RefId;
        public ulong ReferenceTimestamp;
        public ulong OriginateTimestamp;
        public ulong ReceiveTimestamp;
        public ulong TransmitTimestamp;
        public ulong DestinationTimestamp;
        public long Offset;
        public ulong RoundTripTime;
        public ulong RdTscStart;
        public ulong RdTscEnd;
    }

    /// <summary>
    /// Class that initiates an NTP exchange
    /// </summary>
    public class NtpInitiator
    {
        #region Private State
        private uint Period;
        private UdpClient Client;
        private Timer SendTimer;
        private List<Sample> Samples;
        SamplesReady NotifySamples;
        private bool Running;
        private ulong LastRdTscSend;
        private ulong LastRdTscReceive;
        #endregion

        #region Structs and constants
        public const ulong OneHundredNsInOneSecond = 10000000;
        private const ulong MaxUintPlusOne = 0x100000000; // 2^32
        private const ulong MaxUshortPlusOne = 0x10000; // 2^16
        private static DateTime NtpEopch = new DateTime(1900, 1, 1);
        private static DateTime FileTimeEopch = new DateTime(1601, 1, 1);
        #endregion

        #region Packet Processing
        /// <summary>
        /// Add a UINT to a buffer at the designated location
        /// </summary>
        /// <param name="Packet">Packet to modify</param>
        /// <param name="Offset">Start of the uint</param>
        /// <param name="Length">Count of bytes in the uint to be written</param>
        /// <param name="Value">Value to be written</param>
        private void WriteUint(byte[] Packet, uint Offset, uint Length, uint Value)
        {
            for (uint i = Offset + Length - 1; i >= Offset; i--)
            {
                Packet[i] = (byte)Value;
                Value = Value >> 8;
            }
        }

        /// <summary>
        /// Read Length bytes from location Offset and interpret them as a Uint
        /// </summary>
        /// <param name="Packet">Packet to read</param>
        /// <param name="Offset">Offset to read from</param>
        /// <param name="Length">Count of bytes to read</param>
        /// <returns>Value read</returns>
        private uint ReadUint(byte[] Packet, uint Offset, uint Length)
        {
            ulong value = 0;
            for (uint i = Offset; i < Offset + Length; i++)
            {
                value += Packet[i];
                value *= 256;
            }
            value /= 256;
            return (uint)value;
        }

        /// <summary>
        /// Build a client NTP packet 
        /// </summary>
        /// <param name="TransmitTimestamp">Timestamp to put in the packet</param>
        /// <returns>Packet to be sent</returns>
        private byte[] BuildNtpClientPacket(NtpTimeStamp TransmitTimestamp)
        {
            byte[] packet = new byte[48];

            for (int i = 0; i < packet.Length; i++)
            {
                packet[i] = 0;
            }

            packet[0] = 0x23;

            WriteUint(packet, 40, 4, TransmitTimestamp.Seconds);
            // Transmit Timestamp
            WriteUint(packet, 44, 4, TransmitTimestamp.Fraction);
            return packet;
        }

        /// <summary>
        /// Read a field and update the offset
        /// </summary>
        /// <param name="Packet">Packet to read</param>
        /// <param name="Offset">Offset to read from</param>
        /// <param name="Length">Count of bytes to read</param>
        /// <returns>Value read</returns>
        private uint ReadFieldSequential(byte[] Packet, ref uint Offset, uint Length)
        {
            uint value;
            value = ReadUint(Packet, Offset, Length);
            Offset += Length;
            return value;
        }

        /// <summary>
        /// Read a NTP time stamp from the packet at offset
        /// </summary>
        /// <param name="Packet">Packet to pull timestamp from</param>
        /// <param name="Offset">Offset from start of packet</param>
        /// <returns>Timestamp that was read</returns>
        private NtpTimeStamp ReadTimeStampSequenctial(byte[] Packet, ref uint Offset)
        {
            NtpTimeStamp time = new NtpTimeStamp();
            time.Seconds = (uint)ReadFieldSequential(Packet, ref Offset, 4);
            time.Fraction = (uint)ReadFieldSequential(Packet, ref Offset, 4);
            return time;
        }

        /// <summary>
        /// Parse a NTP response packet
        /// </summary>
        /// <param name="Packet">Packet to be parsed</param>
        /// <returns>Resulting sample</returns>
        private Sample ParseNtpServerPacket(byte[] Packet)
        {
            Sample parsedSample = new Sample();
            uint Offset = 0;
            uint flags = ReadFieldSequential(Packet, ref Offset, 1);
            parsedSample.Leap = (byte)(flags >> 6);
            parsedSample.Version = (byte)((flags & 0x1c) >> 3);
            parsedSample.Mode = (byte)(flags & 0x7);

            parsedSample.Stratum = (byte)ReadFieldSequential(Packet, ref Offset, 1);
            parsedSample.PollInterval = (sbyte)ReadFieldSequential(Packet, ref Offset, 1);
            parsedSample.Precision = (sbyte)ReadFieldSequential(Packet, ref Offset, 1);
            parsedSample.RootDelay = ReadFieldSequential(Packet, ref Offset, 4);
            parsedSample.RootDispersion = ReadFieldSequential(Packet, ref Offset, 4);
            parsedSample.RefId = ReadFieldSequential(Packet, ref Offset, 4);
            parsedSample.ReferenceTimestamp = NtpTimeStampToFileTime(ReadTimeStampSequenctial(Packet, ref Offset));
            parsedSample.OriginateTimestamp = NtpTimeStampToFileTime(ReadTimeStampSequenctial(Packet, ref Offset));
            parsedSample.ReceiveTimestamp = NtpTimeStampToFileTime(ReadTimeStampSequenctial(Packet, ref Offset));
            parsedSample.TransmitTimestamp = NtpTimeStampToFileTime(ReadTimeStampSequenctial(Packet, ref Offset));
            return parsedSample;
        }
        #endregion

        #region Time handling
        [DllImport("kernel32.dll")]
        static extern void GetSystemTimePreciseAsFileTime(out FILETIME lpSystemTimeAsFileTime);

        [DllImport("Intrinsics.dll")]
        static extern ulong RdTsc();

        /// <summary>
        /// Query the underlying OS to retrieve the current system file time
        /// </summary>
        /// <returns>System time as file time</returns>
        public static ulong GetSystemTimeAsFileTimeUlong()
        {
            FILETIME fileTimeNow = new FILETIME();
            ulong now;

            // Get the time as a FILETIME
            GetSystemTimePreciseAsFileTime(out fileTimeNow);

            // Unpack filetime
            now = (uint)fileTimeNow.dwHighDateTime;
            now = now << 32;
            now += (uint)fileTimeNow.dwLowDateTime;

            return now;
        }

        /// <summary>
        /// Convert a Windows NT file time to a NTP timestamp
        /// </summary>
        /// <param name="time">Time in 100ns</param>
        /// <returns>Ntp timestamp</returns>
        public static NtpTimeStamp FileTimeToNtpTimeStamp(ulong Time)
        {
            BigInteger fileTime = Time;
            BigInteger epochDelta = (BigInteger)((NtpEopch - FileTimeEopch).TotalSeconds);
            NtpTimeStamp ntpTime = new NtpTimeStamp();
            BigInteger seconds = new BigInteger();
            BigInteger fraction = new BigInteger();

            epochDelta *= OneHundredNsInOneSecond;
            fileTime -= epochDelta;

            seconds = fileTime / OneHundredNsInOneSecond;
            fraction = fileTime % OneHundredNsInOneSecond;
            fraction *= MaxUintPlusOne;
            fraction /= OneHundredNsInOneSecond;

            ntpTime.Seconds = (uint)seconds;

            ntpTime.Fraction = (uint)fraction;
            return ntpTime;
        }

        /// <summary>
        /// Convert a NTP timestamp to a Windows NT file time
        /// </summary>
        /// <param name="NtpTime">NtpTimeStamp</param>
        /// <returns>Filetime</returns>
        public static ulong NtpTimeStampToFileTime(NtpTimeStamp NtpTime)
        {
            BigInteger epochDelta = (BigInteger)((NtpEopch - FileTimeEopch).TotalSeconds);
            epochDelta *= OneHundredNsInOneSecond;
            BigInteger seconds = new BigInteger();
            BigInteger fraction = new BigInteger();

            seconds = NtpTime.Seconds;
            fraction = NtpTime.Fraction;

            seconds *= OneHundredNsInOneSecond;
            fraction *= OneHundredNsInOneSecond;
            fraction /= MaxUintPlusOne;

            return (ulong)(seconds + fraction + epochDelta);
        }

        /// <summary>
        /// Parse a NTP time in the short format
        /// </summary>
        /// <param name="PackedNtpTime">NTP time in short format</param>
        /// <returns>Unpacked NTP Time</returns>
        public static NtpShortTimeInterval ParseNtpShortInterval(uint PackedNtpTime)
        {
            NtpShortTimeInterval ntpTime = new NtpShortTimeInterval();
            ntpTime.Fraction = (ushort)(PackedNtpTime & 0xFFFF);
            ntpTime.Seconds = (ushort)(PackedNtpTime >> 16);
            return ntpTime;
        }

        /// <summary>
        /// Convert a ShortTime structure to file time
        /// </summary>
        /// <param name="NtpTime">The short ntp time to convert</param>
        /// <returns>Corresponding filetime duration</returns>
        public static ulong NtpShortIntervalToFileTime(NtpShortTimeInterval NtpTime)
        {
            BigInteger fileTime = NtpTime.Fraction;
            fileTime *= OneHundredNsInOneSecond;
            fileTime /= MaxUshortPlusOne;
            fileTime += NtpTime.Seconds * OneHundredNsInOneSecond;
            return (ulong)(fileTime);
        }

        #endregion

        #region Socket events
        /// <summary>
        /// Send a NTP client packet when the timer expires
        /// </summary>
        /// <param name="state">Unused</param>
        private void SendCallback(object state)
        {
            byte[] Packet = BuildNtpClientPacket(FileTimeToNtpTimeStamp(GetSystemTimeAsFileTimeUlong()));
            LastRdTscSend = RdTsc();
            Client.Send(Packet, Packet.Length);
            if (Running)
            {
                SendTimer.Change(Period, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Parse  the NTP server packet when it arrives.
        /// </summary>
        /// <param name="state">Unused</param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            LastRdTscReceive = RdTsc();
            try
            {
                IPEndPoint remoteEp = null;
                int Count = 0; ;
                byte[] Packet = Client.EndReceive(ar, ref remoteEp);
                if (Packet.Length >= 48)
                {
                    Sample s = ParseNtpServerPacket(Packet);
                    s.DestinationTimestamp = GetSystemTimeAsFileTimeUlong();
                    lock (Samples)
                    {
                        ulong t1 = s.OriginateTimestamp;
                        ulong t2 = s.ReceiveTimestamp;
                        ulong t3 = s.TransmitTimestamp;
                        ulong t4 = s.DestinationTimestamp;
                        s.Offset = (((long)t2 - (long)t1) + ((long)t3 - (long)t4)) / 2;
                        s.RoundTripTime = (t4 - t1) - (t2 - t2);
                        s.RdTscStart = LastRdTscSend;
                        s.RdTscEnd = LastRdTscReceive;
                        Samples.Add(s);
                        Count = Samples.Count;
                    }
                    if (NotifySamples != null)
                    {
                        NotifySamples(s);
                    }
                }
                if (Running)
                {
                    Client.BeginReceive(ReceiveCallback, null);
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                Running = false;
            }
        }
        #endregion

        #region Public APIs

        public delegate void SamplesReady(Sample sample);

        /// <summary>
        /// Start pinging the specified server with the period provided
        /// </summary>
        /// <param name="Server">Server to ping</param>
        /// <param name="Period">Frequency of ping</param>
        public NtpInitiator(string Server, uint Period)
        {
            this.Period = Period;
            Samples = new List<Sample>();
            Client = new UdpClient(AddressFamily.InterNetwork);
            Client.Connect(Server, 123);
            SendTimer = new Timer(SendCallback);
            Running = false;
        }

        /// <summary>
        /// Start sending NTP requests
        /// </summary>
        public void Start()
        {
            Running = true;
            SendTimer.Change(0, Timeout.Infinite);
            Client.BeginReceive(ReceiveCallback, null);
        }

        /// <summary>
        /// Stop sending NTP request
        /// </summary>
        public void Stop()
        {
            Running = false;
        }

        /// <summary>
        /// Dump all the samples
        /// </summary>
        public void ClearSamples()
        {
            lock (Samples)
            {
                Samples.Clear();
            }
        }

        /// <summary>
        /// Compute stats from samples
        /// </summary>
        /// <param name="OffsetMedian">Median offset</param>
        /// <param name="OffsetDeviation">STDEV of offset</param>
        /// <param name="RoundTripMedian">Median of RTT</param>
        /// <param name="RoundTripDeviation">STDEV of RTT</param>
        public bool ComputeSampleStats(out double OffsetMedian, out double OffsetDeviation, out double RoundTripMedian, out double RoundTripDeviation)
        {
            List<BigInteger> offsetList = new List<BigInteger>();
            List<BigInteger> delayList = new List<BigInteger>();
            BigInteger Σoffset = 0;
            BigInteger Σdelay = 0;
            BigInteger offsetAvg = 0;
            BigInteger delayAvg = 0;
            BigInteger offDev = 0;
            BigInteger rttDev = 0;
            int n = 0;
            lock (Samples)
            {
                if (Samples.Count == 0)
                {
                    OffsetMedian = 0;
                    OffsetDeviation = 0;
                    RoundTripMedian = 0;
                    RoundTripDeviation = 0;
                    return false;
                }
                foreach (Sample s in Samples)
                {
                    Σoffset += s.Offset;
                    Σdelay += s.RoundTripTime;
                    offsetList.Add(s.Offset);
                    delayList.Add(s.RoundTripTime);
                    n = delayList.Count;
                }
            }

            offsetList.Sort();
            delayList.Sort();
            OffsetMedian = (double)(offsetList[n / 2]) / OneHundredNsInOneSecond;
            RoundTripMedian = (double)(delayList[n / 2]) / OneHundredNsInOneSecond;

            offsetAvg = Σoffset / n;
            delayAvg = Σdelay / n;

            OffsetDeviation = 0;
            RoundTripDeviation = 0;

            foreach (BigInteger o in offsetList)
            {
                offDev += (offsetAvg - o) * (offsetAvg - o);
            }
            foreach (BigInteger d in delayList)
            {
                rttDev += (delayAvg - d) * (delayAvg - d);
            }
            OffsetDeviation = Math.Sqrt((double)(offDev / n)) / OneHundredNsInOneSecond;
            RoundTripDeviation = Math.Sqrt((double)(rttDev / n)) / OneHundredNsInOneSecond;
            return true;
        }

        /// <summary>
        /// Get additional information that was returned in the NTP packet
        /// </summary>
        /// <param name="RefId">ReferenceId from the packet</param>
        /// <param name="Stratum">Server's reported stratum number</param>
        /// <returns>True if we have at least one sample</returns>
        public bool GetServerMetadata(
            out uint Leap,
            out uint Version,
            out uint Mode,
            out uint Stratum,
            out double PollInterval,
            out double Precision,
            out ulong RootDelay,
            out ulong RootDispersion,
            out string RefId)
        {
            uint localRefId;
            Sample s;
            Leap = 0;
            Version = 0;
            Mode = 0;
            RefId = "";
            PollInterval = 0;
            Stratum = 0;
            Precision = 0;
            RootDelay = 0;
            RootDispersion = 0;

            lock (Samples)
            {
                if (Samples.Count == 0)
                {
                    return false;
                }
                s = Samples[0];
            }

            Leap = s.Leap;
            Version = s.Version;
            Mode = s.Mode;
            Stratum = s.Stratum;
            PollInterval = Math.Pow(2, s.PollInterval);
            Precision = Math.Pow(2, s.Precision);
            RootDelay = NtpShortIntervalToFileTime(ParseNtpShortInterval(s.RootDelay));
            RootDispersion = NtpShortIntervalToFileTime(ParseNtpShortInterval(s.RootDispersion));

            RefId = "";
            localRefId = s.RefId;
            byte[] RefIdBytes = new byte[4];
            for (int i = 3; i >= 0; i--)
            {
                RefIdBytes[i] = (byte)(localRefId & 0xff);
                localRefId = localRefId >> 8;
            }

            foreach (byte b in RefIdBytes)
            {
                if (s.Stratum == 1)
                {
                    RefId += Convert.ToChar(b);
                }
                else
                {
                    RefId += b.ToString() + ".";
                }
            }
            RefId = RefId.TrimEnd('.');

            return true;
        }

        /// <summary>
        /// Set the handler that is invoked when samples are recieved
        /// </summary>
        /// <param name="SampleDelegate">Delegate to invoke when samples are received</param>
        public void SetSampleReady(SamplesReady SampleDelegate)
        {
            lock (Samples)
            {
                NotifySamples = SampleDelegate;
            }
        }

        /// <summary>
        /// Grab a record of all samples 
        /// </summary>
        /// <returns>Array of all the samples</returns>
        public Sample[] GetSamples()
        {
            lock (Samples)
            {
                return Samples.ToArray();
            }
        }
        #endregion
    }
}
