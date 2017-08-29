// ntpcli.cpp : Command line utility to poll an NTP server and log the results
//

#include "stdafx.h"
#include <iostream>
#include <chrono>
#include <thread>
#include <map>
#include <string>
#include <vector>

#include "platform.h"
#include "ntp.h"

std::map<std::string, std::string> ParseCommandLine(int argc, char ** argv)
{
    std::map<std::string, std::string> argPairs;
    std::string argName;
    std::string argValue;
    for (size_t i = 1; i < argc; i++)
    {
        if (argv[i][0] == '-' || argv[i][0] == '/')
        {
            argName = argv[i] + 1;
            for (auto & c : argName)
            {
                c = tolower(c);
            }

        }
        else if (argName.length() > 0)
        {
            argValue = std::string(argv[i]);
            for (auto & c : argValue)
            {
                c = tolower(c);
            }
            argPairs.insert(std::make_pair(argName, argValue));
            argName.clear();
        }
    }
    return argPairs;
}

int main(int argc, char ** argv)
{

    unsigned long interval;
    long long sendTime;
    addrinfo * addr = nullptr;
    int err;
    SOCKET s;
    enum {
        Short,
        Long
    } Form = Short;

    PlatformInit();

    // Parse the command line
    std::map<std::string, std::string> args = ParseCommandLine(argc, argv);
    
    if (args.find("host") == args.end() ||
        args.find("interval") == args.end())
    {
        printf("usage: %s -host <name> -interval <seconds> -form <short/long>\n", argv[0]);
        exit(-1);
    }

    interval = atoi(args["interval"].c_str());

    if (args.find("form") != args.end())
    {
        if (args["form"] == "short")
        {
            Form = Short;
        }
        else if (args["form"] == "long")
        {
            Form = Long;
        }
    }

    // Print the header line for the CSV if this is the long form
    switch (Form)
    {
    case Long:
        printf("ip,recvTime,LeapIndicator,Version,Stratum,Poll,Precision,RootDelay,RootDispersion,Reference,ReceiveTx,TransmitTx\n");
        break;
    case Short:
        break;
    }

    // Get the list of addresses for this host
    err = getaddrinfo(args["host"].c_str(), "123", nullptr, &addr);
    if (err != 0)
    {
        printf("getaddrinfo failed %d\n", err);
        exit(err);
    }

    // Create the socket we send and receive on
    s = socket(addr->ai_family, SOCK_DGRAM, IPPROTO_UDP);
    if (s == INVALID_SOCKET)
    {
        printf("socket failed %d\n", MyGetLastError());
        exit(err);
    }

    // Start sending NTP request
    auto senderThread = std::thread([&] {
        int err;
        NtpPacket request{ 0 };
        request.Version = 4;
        request.Mode = 3;
        std::vector<unsigned char> buffer;
        PushBack(buffer, request);
        for (;;)
        {
            sendTime = std::chrono::high_resolution_clock::now().time_since_epoch().count();
            err = sendto(s, (char*)buffer.data(), static_cast<int>(buffer.size()), 0, addr->ai_addr, static_cast<int>(addr->ai_addrlen));
            if (err == SOCKET_ERROR)
            {
                printf("sendto failed %d\n", MyGetLastError());
                exit(-1);
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(5000));
        }
    });

    std::this_thread::sleep_for(std::chrono::milliseconds(1000));

    // Start receiving NTP responses
    auto recvThread = std::thread([&] {
        for (;;)
        {
            NtpPacket response{ 0 };
            std::vector<unsigned char> buffer(128);
            char addressBuffer[128];
            sockaddr* r = (sockaddr*)addressBuffer;
            socklen_t rLen = sizeof(buffer);
            char ip[50] = { 0 };
            size_t offset = 0;
            char reference[128] = { 0 };

            // Wait for an NTP response packet
            int err = recvfrom(s, reinterpret_cast<char*>(buffer.data()), static_cast<int>(buffer.size()), 0, r, &rLen);
            long long recvTime = std::chrono::high_resolution_clock::now().time_since_epoch().count();
            if (err == SOCKET_ERROR)
            {
                printf("recvfrom failed %d\n", MyGetLastError());
                exit(-1);
            }

            // Unpack the NTP response
            Extract(buffer, offset, response);

            // Format the reponders IP address as a string
            switch (r->sa_family)
            {
                case AF_INET:
                {
                    sockaddr_in* a = reinterpret_cast<sockaddr_in*>(r);
                    inet_ntop(AF_INET, &a->sin_addr, ip, sizeof(ip));
                }
                break;
                case AF_INET6:
                {
                    sockaddr_in6* a = reinterpret_cast<sockaddr_in6*>(r);
                    inet_ntop(AF_INET6, &a->sin6_addr, ip, sizeof(ip));
                }
                break;
            }

            // If this is a straum 1 clock, print the refid as text
            if (response.Stratum == 1)
            {
                reference[0] = response.ReferenceId[0];
                reference[1] = response.ReferenceId[1];
                reference[2] = response.ReferenceId[2];
                reference[3] = response.ReferenceId[3];
            }
            else
            {
                inet_ntop(AF_INET, &response.ReferenceId, reference, sizeof(reference));
            }

            switch (Form)
            {
            case Short:
                printf("%llu,%lld,%lld\n",
                    sendTime,
                    recvTime,
                    NtpTimeStampToFileTime(response.Transmit) / 2 + NtpTimeStampToFileTime(response.Receive) / 2
                );
                break;
            case Long:
                printf("%s,%llu,%llu,%lu,%lu,%lu,%ld,%ld,0.%.6lu,0.%.6lu,%s,%lld,%lld\n",
                    ip,
                    sendTime,
                    recvTime,
                    (unsigned long)response.LeapIndicator,
                    (unsigned long)response.Version,
                    (unsigned long)response.Stratum,
                    (unsigned long)response.Poll,
                    (long)response.Precision,
                    NtpShortFormToNanoSecond(response.RootDelay) / 1000,
                    NtpShortFormToNanoSecond(response.RootDispersion) / 1000,
                    reference,
                    NtpTimeStampToFileTime(response.Receive),
                    NtpTimeStampToFileTime(response.Transmit)
                );
                break;
            }
        }
    });
    std::this_thread::sleep_for(std::chrono::seconds(interval));

    exit(0);

    return 0;
}

