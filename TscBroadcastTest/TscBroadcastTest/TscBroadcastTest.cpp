// TscBroadcastTest.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <atomic>
#include <thread>
#include <vector>
#include <algorithm>

void CollectSamples(std::atomic<bool> & Signal, bool Client, std::vector<unsigned long long> & Samples)
{
    unsigned int i;
    for (size_t index = 0; index < Samples.size(); index++)
    {
        while (Signal.load() != Client)
        {
        }
        unsigned long long ts = __rdtscp(&i);
        Signal.store(!Client);
        Samples[index] = ts;
    }
}

void ComputeStats(std::vector<long long> Samples, long long & Mean, long long & Median, long long & StdDev)
{
    Mean = 0;
    Median = 0;
    StdDev = 0;
    std::sort(Samples.begin(), Samples.end());
    std::for_each(Samples.begin(), Samples.end(), [&](long long Sample)
    {
        Mean += Sample;
    });
    Mean /= static_cast<long long>(Samples.size());
    std::for_each(Samples.begin(), Samples.end(), [&](long long Sample) 
    {
        StdDev += (Sample - Mean) * (Sample - Mean);
    });
    StdDev /= static_cast<long long>(Samples.size());
    StdDev = std::sqrt(StdDev);
    Median = Samples[Samples.size() / 2];
}

int main(int argc, char ** argv)
{
    size_t serverCpuId = atoi(argv[1]);
    size_t clientCpuId = atoi(argv[2]);
    size_t samples = atoi(argv[3]);
    CACHE_ALIGN(std::vector<unsigned long long> tsClient(samples));
    CACHE_ALIGN(std::vector<unsigned long long> tsServer(samples));
    CACHE_ALIGN(std::atomic<bool> clientOwns);
    printf("O-Mean\tO-Med\tO-STDEV\tR-Mean\tR-Med\tR-STDEV\n");
    for (size_t i = 0; i < 10; i++)
    {
        clientOwns.store(false);
        auto client = std::thread([&tsClient, &clientOwns, samples, clientCpuId]() {
            SetThreadAffinity(clientCpuId);
            CollectSamples(clientOwns, true, tsClient);
        });
        auto server = std::thread([&tsServer, &clientOwns, samples, serverCpuId]() {
            SetThreadAffinity(serverCpuId);
            CollectSamples(clientOwns, false, tsServer);
        });
        client.join();
        server.join();

        std::vector<long long> offsets;
        std::vector<long long> rtts;
        long long avgOffset = 0;
        for (size_t i = 0; i < samples - 1; i++)
        {
            long long offset = (2 * (long long)tsClient[i] - (long long)tsServer[i] - (long long)tsServer[i + 1]) / 2;
            long long rtt = (long long)tsServer[i + 1] - (long long)tsServer[i];
            offsets.push_back(offset);
            rtts.push_back(rtt);
        }

        long long mean, median, stddev;
        ComputeStats(offsets, mean, median, stddev);
        printf("%I64i\t%I64i\t%I64i\t", mean, median, stddev);
        ComputeStats(rtts, mean, median, stddev);
        printf("%I64i\t%I64i\t%I64i\n", mean, median, stddev);
    }
}