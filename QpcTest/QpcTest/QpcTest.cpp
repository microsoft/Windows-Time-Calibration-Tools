// QpcTest.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <Windows.h>

int main()
{
	for (int j = 0; j < 10; j++)
	{
		FILETIME ft;

		LARGE_INTEGER ts1, ts2, freq;
		__int64 total = 0;
		QueryPerformanceCounter(&ts1);
		for (long long i = 0; i < 1000000000; i++)
		{
			GetSystemTimePreciseAsFileTime(&ft);
		}
		QueryPerformanceCounter(&ts2);
		total += ts2.QuadPart - ts1.QuadPart;

		QueryPerformanceFrequency(&freq);
		double queryTime = total;
		queryTime /= freq.QuadPart;
		printf("%.3f\n", queryTime);
	}

	return 0;
}

