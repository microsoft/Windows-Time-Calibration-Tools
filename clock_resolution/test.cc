#include <iostream>
#include <chrono>
#include <algorithm>
#include <vector>

#if defined(_MSC_VER)
#include <windows.h>
#include <intrin.h>
#undef min

    inline bool SetThreadAffinity(size_t CpuId)
    {
        DWORD_PTR affinityMask = 1ull << CpuId;
        if (!SetThreadAffinityMask(GetCurrentThread(), affinityMask))
        {
            return false;
        }
        return true;
    }
#else
#include <pthread.h>
    inline bool SetThreadAffinity(size_t CpuId)
    {
        cpu_set_t cpuset;
        pthread_t thread;
        thread = pthread_self();
        CPU_ZERO(&cpuset);
        CPU_SET(CpuId, &cpuset);
        if (0 != pthread_setaffinity_np(thread, sizeof(cpu_set_t), &cpuset))
        {
            return false;
        }
        CPU_ZERO(&cpuset);
        if (0 != pthread_getaffinity_np(thread, sizeof(cpu_set_t), &cpuset) ||
            (!CPU_ISSET(CpuId, &cpuset)))
        {
            return false;
        }
        return true;
    }
#endif

double StdDevAsFractionOfMean(const std::vector<unsigned long long> & Samples)
{
    double mean = 0;
    double err = 0;
    unsigned long long previous = Samples[0];
    for (size_t i = 1; i < Samples.size(); i++)
    {
        mean += Samples[i] - previous;
        previous = Samples[i];
    }
    mean /= Samples.size();
    previous = Samples[0];
    for (size_t i = 1; i < Samples.size(); i++)
    {
        double delta = static_cast<double>(Samples[i] - previous);
        delta -= mean;
        err += delta * delta;
        previous = Samples[i];
    }
    err /= Samples.size();
    err = sqrt(err);
    return err / mean;
}

template <typename clock>
unsigned long long MeasureClockResolution()
{
    const size_t iteration = 1000000;
    long long shortest = 0;
    auto start = clock::now();
    auto end = clock::now();
    for (size_t i = 0; i < iteration; i++)
    {
        end = clock::now();
        if (end != start)
        {
            long long delta = std::chrono::duration_cast<std::chrono::nanoseconds>(end - start).count();
            shortest = shortest == 0 ? delta : std::min(shortest, delta);
            start = clock::now();
        }
    }

    return shortest;
}

template <typename clock>
void MeasureTimeStampLatency(long long & Latency, long long & StDev)
{
    const size_t iteration = 100000000;
    std::vector<unsigned long long> timeStamps(iteration);
    auto start = clock::now();
    auto end = clock::now();
    for (auto & ts : timeStamps)
    {
        end = clock::now();
        ts = __rdtsc();
    }
    Latency = std::chrono::duration_cast<std::chrono::nanoseconds>(end - start).count() / iteration;
    StDev = (long long)(StdDevAsFractionOfMean(timeStamps) * Latency);
}

int main()
{
    SetThreadAffinity(0);
    std::cout << "std::chrono::high_resolution_clock resolution on this platform is: " << MeasureClockResolution<std::chrono::high_resolution_clock>() << "ns" << std::endl;
    std::cout << "std::chrono::system_clock resolution on this platform is: " << MeasureClockResolution<std::chrono::system_clock>() << "ns" << std::endl;
    std::cout << "std::chrono::steady_clock resolution on this platform is: " << MeasureClockResolution<std::chrono::steady_clock>() << "ns" << std::endl;
    long long latency;
    long long stdev;
    MeasureTimeStampLatency<std::chrono::high_resolution_clock>(latency, stdev);
    std::cout << "Timestamp latency for std::chrono::high_resolution_clock resolution on this platform is: " << latency << "ns with STDEV " << stdev << "ns" << std::endl;
    MeasureTimeStampLatency<std::chrono::system_clock>(latency, stdev);
    std::cout << "Timestamp latency for std::chrono::system_clock resolution on this platform is: " << latency << "ns with STDEV " << stdev << "ns" << std::endl;
    MeasureTimeStampLatency<std::chrono::steady_clock>(latency, stdev);
    std::cout << "Timestamp latency for std::chrono::steady_clock resolution on this platform is: " << latency << "ns with STDEV " << stdev << "ns" << std::endl;

    return 0;
}
