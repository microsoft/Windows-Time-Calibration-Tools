// TscOffset.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <intrin.h>
#include <thread>
#include <atomic>
#include "platform.h"

volatile struct Message {
    enum eState {
        Idle = 0,
        ClientPrep = 1,
        ClientDone = 2,
        ServerPrep = 3,
        ServerDone = 4
    };

    __declspec(align(64)) struct {
        long long T1;
        long long T4;
    };
    __declspec(align(64)) struct {
        long long T2;
        long long T3;
    };
    __declspec(align(64))struct {
        std::atomic<size_t> State;
    };
} Msg = {};

void Server(size_t CpuId)
{
    SetThreadAffinity(CpuId);
    unsigned int i;
    for (;;)
    {
        size_t test = Message::ClientDone;
        if (!Msg.State.compare_exchange_weak(test, Message::ServerPrep))
        {
            continue;
        }
        Msg.T2 = __rdtscp(&i);
        Msg.T3 = __rdtscp(&i);
        Msg.State.exchange(Message::ServerDone);
    }
}

void Client(size_t CpuId, long RttBound, long long & Offset, long long & Rtt)
{
    SetThreadAffinity(CpuId);
    unsigned int i;
    for (;;)
    {
        size_t test = Message::Idle;
        if (!Msg.State.compare_exchange_weak(test, Message::ClientPrep))
        {
            continue;
        }
        Msg.T1 = __rdtscp(&i);
        Msg.State.exchange(Message::ClientDone);
        test = Message::ServerDone;
        while (!Msg.State.compare_exchange_weak(test, Message::Idle))
        {
            test = Message::ServerDone;
        }

        Msg.T4 = __rdtscp(&i);

        Offset = ((Msg.T2 - Msg.T1) + (Msg.T3 - Msg.T4)) / 2;
        Rtt = (Msg.T4 - Msg.T1) - (Msg.T3 - Msg.T2);

        if (Msg.T2 > Msg.T3)
        {
            continue;
        }

        if (Msg.T1 > Msg.T4)
        {
            continue;
        }

        if (Rtt > RttBound)
        {
            continue;
        }
        break;
    }
}

int main(int argc, char ** argv)
{ 
    if (argc != 5)
    {
        printf("Usage: %s Server Client Iterations Cutoff\n", argv[0]);
        exit(-1);
    }

    int serverCpuId = atoi(argv[1]);
    int clientCpuId = atoi(argv[2]);
    int iterations = atoi(argv[3]);
    int rttBounds = atoi(argv[4]);

    std::thread t([&]() { Server(serverCpuId);  });
    for (size_t i = 0; i < 10; i++)
    {
        long long averageOffset = 0;
        long long averageRtt = 0;
        
        for (size_t j = 0; j < iterations; j++)
        {
            long long offset = 0;
            long long rtt = 0;
            Client(clientCpuId, rttBounds, offset, rtt);
            averageOffset += offset;
            averageRtt += rtt;
        }
        averageOffset /= iterations;
        averageRtt /= iterations;
        printf("%lld\t%lld\n", averageOffset, averageRtt);
    }
    exit(0);
    return 0;
}

