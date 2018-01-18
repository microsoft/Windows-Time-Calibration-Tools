// TscOffset.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <intrin.h>
#include <thread>
#include <atomic>
#include <Windows.h>

struct Message {
    unsigned long long ClientTx;
    unsigned long long ClientRx;
    unsigned long long ServerRx;
    unsigned long long ServerTx;
};

size_t serverHits = 0;
size_t clientHits = 0;
void Server(size_t CpuId, volatile Message * msg)
{
    DWORD_PTR mask = 1 << CpuId;
    SetThreadAffinityMask(GetCurrentThread(), mask);
    unsigned long long oldClientTx = 0;
    for (;;)
    {
        unsigned int i;
        unsigned long long ts = __rdtscp(&i);
        unsigned long long currentClientTx = msg->ClientTx;
        if (currentClientTx == oldClientTx)
        {
            continue;
        }
        oldClientTx = currentClientTx;
        msg->ServerRx = ts;
        std::atomic_thread_fence(std::memory_order_release);
        msg->ServerTx = __rdtscp(&i);
        serverHits++;
    }
}

void Client(size_t CpuId, volatile Message * msg)
{
    DWORD_PTR mask = 1 << CpuId;
    SetThreadAffinityMask(GetCurrentThread(), mask);
    unsigned long long oldServerTx = msg->ServerTx;
    unsigned int i;
    unsigned long long tx = __rdtscp(&i);
    msg->ClientTx = tx;
    std::atomic_thread_fence(std::memory_order_release);
    for (;;)
    {
        unsigned int i;
        unsigned long long currentServerTx = msg->ServerTx;
        if (currentServerTx == oldServerTx)
        {
            continue;
        }
        
        std::atomic_thread_fence(std::memory_order_release);
        msg->ClientRx = __rdtscp(&i);
        break;
    }
    clientHits++;
}

int main(int argc, char ** argv)
{ 
    volatile Message m = { 0 };
    //int server = atoi(argv[1]);
    //int client = atoi(argv[2]);
    std::thread t([&]() { Server(0, &m);  });

    for (size_t j = 0; j < 10; j++)
    {
        long long averageDelay = 0;
        long long averageRtt = 0;
        long long averageServerTime = 0;
        size_t iterations = 1000000;
        for (size_t i = 0; i < iterations; i++)
        {

            Client(1, &m);
            //printf("%lld,%lld,%lld,%lld\n", m.ClientTx, m.ServerRx, m.ServerTx, m.ClientRx);
            long long delta = (m.ServerRx / 2 - m.ClientTx / 2) + (m.ServerTx / 2 - m.ClientRx / 2);
            long long rtt = (m.ClientRx - m.ClientTx) - (m.ServerTx - m.ServerRx);
            long long cli = (m.ClientRx - m.ClientTx);
            long long srv = (m.ServerTx - m.ServerRx);
            if (cli < srv)
            {
                printf("oops\n");
            }
            averageServerTime += srv;
            averageDelay += delta;
            averageRtt += rtt;
        }
        averageDelay /= iterations;
        averageRtt /= iterations;
        printf("%d\t%d,%d\n", (long)averageDelay, (long)averageRtt, (long)averageServerTime);
    }
    exit(0);
    return 0;
}

