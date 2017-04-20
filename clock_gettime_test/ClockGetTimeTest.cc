/*++

Copyright (c) Microsoft Corporation

Module Name:

Latency

Abstract:

This module captures the latency of the Linux time APIs

Author:

Alan Jowett (alanjo) 19-March-2016

--*/

#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include <time.h>
#include <string.h>
typedef unsigned long long DWORD64;


unsigned long long __rdtsc()
{
	unsigned long low;
	unsigned long high;
	asm volatile("rdtsc" : "=a" (low), "=d" (high));
	return low | ((unsigned long long)high) << 32;
}

double StdDevAsFractionOfMean(DWORD64 * Samples, size_t SampleSize)
{
	double mean = 0;
	double err = 0;
	DWORD64 previous = Samples[0];
	for (size_t i = 1; i < SampleSize; i++)
	{
		mean += Samples[i] - previous;
		previous = Samples[i];
	}
	mean /= SampleSize;
	previous = Samples[0];
	for (size_t i = 1; i < SampleSize; i++)
	{
		double delta = Samples[i] - previous;
		delta -= mean;
		err += delta * delta;
		previous = Samples[i];
	}
	err /= SampleSize;
	err = sqrt(err);
	return err / mean;
}

double TimeFromTimeSpec(timespec t)
{
	double time = t.tv_nsec;
	time /= 1e9;
	time += t.tv_sec;
        time /= 1e9;
	return time;
}


void ScaleAndPrintResults(timespec Start, timespec End, size_t SampleSize, DWORD64* Samples, const char * Name)
{

	double queryTime = TimeFromTimeSpec(End) - TimeFromTimeSpec(Start);
	queryTime *= 1e9;
	double stdev = StdDevAsFractionOfMean(Samples, SampleSize) * queryTime;
	printf("%s latency %.1fns STDEV %.1fns\n", Name, queryTime, stdev);
}

int main(int argc, char ** argv)
{
	if (argc != 3) {
		printf("%s samples_size iterations\n", argv[0]);
		exit(-1);
	}

	size_t sampleSize = atoll(argv[1]);
	size_t iterations = atol(argv[2]);
	DWORD64* samples = new DWORD64[sampleSize];
	memset(samples, 0, sizeof(DWORD64) * sampleSize);


	for (int j = 0; j < iterations; j++)
	{
		timespec ts;
		timespec start, end;
		clock_gettime(CLOCK_REALTIME, &start);
		for (long long i = 0; i < sampleSize; i++)
		{
			clock_gettime(CLOCK_REALTIME, &ts);
			samples[i] = __rdtsc();
		}
		clock_gettime(CLOCK_REALTIME, &end);
		ScaleAndPrintResults(start, end, sampleSize, samples, "clock_gettime");
	}

	for (int j = 0; j < iterations; j++)
	{
		timespec start, end;
		clock_gettime(CLOCK_REALTIME, &start);
		for (long long i = 0; i < sampleSize; i++)
		{
			samples[i] = __rdtsc();
		}
		clock_gettime(CLOCK_REALTIME, &end);
		ScaleAndPrintResults(start, end, sampleSize, samples, "__rdtsc");
	}

	return 0;
}


