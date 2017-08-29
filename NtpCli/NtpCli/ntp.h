#pragma once
struct NtpTimeStamp
{
    unsigned long Seconds;
    unsigned long Fraction;
};

struct NtpShortFormat
{
    unsigned short Seconds;
    unsigned short Fraction;
};

struct NtpPacket {
    unsigned char LeapIndicator : 2;
    unsigned char Version : 3;
    unsigned char Mode : 3;
    unsigned char Stratum;
    unsigned char Poll;
    char Precision;
    NtpShortFormat RootDelay;
    NtpShortFormat RootDispersion;
    unsigned char ReferenceId[4];
    NtpTimeStamp Reference;
    NtpTimeStamp Origin;
    NtpTimeStamp Receive;
    NtpTimeStamp Transmit;
};

void PushBack(std::vector<unsigned char> & Buffer, unsigned long Value)
{
    Buffer.push_back((unsigned char)(Value >> 24));
    Buffer.push_back((unsigned char)(Value >> 16));
    Buffer.push_back((unsigned char)(Value >> 8));
    Buffer.push_back((unsigned char)(Value >> 0));
}

void PushBack(std::vector<unsigned char> & Buffer, unsigned short Value)
{
    Buffer.push_back((unsigned char)(Value >> 8));
    Buffer.push_back((unsigned char)(Value >> 0));
}

template<size_t s>
void PushBack(std::vector<unsigned char>& Buffer, unsigned char Value[s])
{
    for (size_t i = 0; i < s; i++)
    {
        Buffer.push_back(Value[i]);
    }
}

void PushBack(std::vector<unsigned char> & Buffer, NtpShortFormat Value)
{
    PushBack(Buffer, Value.Seconds);
    PushBack(Buffer, Value.Fraction);
}

void PushBack(std::vector<unsigned char> & Buffer, NtpTimeStamp Value)
{
    PushBack(Buffer, Value.Seconds);
    PushBack(Buffer, Value.Fraction);
}


void PushBack(std::vector<unsigned char> & Buffer, NtpPacket & Packet)
{
    unsigned char flags = Packet.LeapIndicator << 6 | Packet.Version << 2 | Packet.Mode;
    Buffer.push_back(flags);
    Buffer.push_back(Packet.Stratum);
    Buffer.push_back(Packet.Poll);
    Buffer.push_back((unsigned char)Packet.Precision);
    PushBack(Buffer, Packet.RootDelay);
    PushBack(Buffer, Packet.RootDispersion);
    PushBack<4>(Buffer, Packet.ReferenceId);
    PushBack(Buffer, Packet.Reference);
    PushBack(Buffer, Packet.Origin);
    PushBack(Buffer, Packet.Receive);
    PushBack(Buffer, Packet.Transmit);
}

void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, unsigned char & Value)
{
    Value = Buffer[Offset++];
}

void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, char & Value)
{
    Value = Buffer[Offset++];
}

template<size_t s>
void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, unsigned char Value[s])
{
    for (size_t i = 0; i < s; i++)
    {
        Value[i] = Buffer[Offset++];
    }
}

void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, unsigned long & Value)
{
    Value = 0;
    Value += ((unsigned long)Buffer[Offset++]) << 24;
    Value += ((unsigned long)Buffer[Offset++]) << 16;
    Value += ((unsigned long)Buffer[Offset++]) << 8;
    Value += ((unsigned long)Buffer[Offset++]) << 0;
}

void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, unsigned short & Value)
{
    Value = 0;
    Value += Buffer[Offset++] << 8;
    Value += Buffer[Offset++] << 0;
}

void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, NtpShortFormat & Value)
{
    Extract(Buffer, Offset, Value.Seconds);
    Extract(Buffer, Offset, Value.Fraction);
}

void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, NtpTimeStamp & Value)
{
    Extract(Buffer, Offset, Value.Seconds);
    Extract(Buffer, Offset, Value.Fraction);
}

void Extract(std::vector<unsigned char> & Buffer, size_t & Offset, NtpPacket & Packet)
{
    unsigned char flags;
    Extract(Buffer, Offset, flags);
    Packet.LeapIndicator = flags >> 6;
    Packet.Version = flags >> 3;
    Packet.Mode = flags;
    Extract(Buffer, Offset, Packet.Stratum);
    Extract(Buffer, Offset, Packet.Poll);
    Extract(Buffer, Offset, Packet.Precision);
    Extract(Buffer, Offset, Packet.RootDelay);
    Extract(Buffer, Offset, Packet.RootDispersion);
    Extract<4>(Buffer, Offset, Packet.ReferenceId);
    Extract(Buffer, Offset, Packet.Reference);
    Extract(Buffer, Offset, Packet.Origin);
    Extract(Buffer, Offset, Packet.Receive);
    Extract(Buffer, Offset, Packet.Transmit);
}

// Convert NtpTimeStamp fraction to ns units
unsigned long long NtpTimeStampToFileTime(NtpTimeStamp & TimeStamp)
{
    unsigned long long fraction = 1000000000ull; // ns in seconds
    unsigned long long seconds = 9434620800 + TimeStamp.Seconds;

    fraction *= TimeStamp.Fraction;
    fraction /= 0x100000000;
    return seconds * 10000000 + fraction / 100;
}

// Convert NtpShortFormat fraction to ns units
unsigned long NtpShortFormToNanoSecond(NtpShortFormat ntp)
{
    unsigned long long ns = 1000000000ull;
    ns *= ntp.Fraction;
    ns /= 0x10000;
    ns += ((unsigned long long)ntp.Seconds) * 1000000000ull;
    return static_cast<unsigned long>(ns);
}
