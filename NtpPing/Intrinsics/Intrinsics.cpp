// Intrinsics.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

extern "C" {
unsigned __int64 
RdTscWrapper(
    );

void 
CpuIdWrapper(
   int cpuInfo[4],
   int function_id
   );

void 
CpuIdExWrapper(
   int cpuInfo[4],
   int function_id,
   int subfunction_id
   );
}

unsigned __int64 RdTscWrapper()
{
    return __rdtsc();
}

void CpuIdWrapper(
   int cpuInfo[4],
   int function_id)
{
    __cpuid(cpuInfo, function_id);
}

void CpuIdExWrapper(
   int cpuInfo[4],
   int function_id,
   int subfunction_id)
{
    __cpuidex(cpuInfo, function_id, subfunction_id);
}
