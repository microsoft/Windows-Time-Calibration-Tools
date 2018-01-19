#pragma once
#if defined(_MSC_VER)
#include <windows.h>
#include <intrin.h>
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
