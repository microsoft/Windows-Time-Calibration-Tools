# Windows-Time-Calibration-Tools
## Window Tools for Calibrating Windows Time Service

The set of tools included are designed to measure and calibrate the Windows time (or for that matter any time) service.  It consists of various tools, some of which support the higher level functions.  At a high level, they provide the following functions:

* *NtpMonitor Service* - Identifies machines to monitor via registry entries which then sends NTP requests.  The service uses regular NTP messages and produces logs.  The log files can be analyze and charted using the GenerateGraphs.ps1 script in the Scripts directly.
* *Create-MonitorCharts.ps1* - A PowerShell script which produces graphs to observe the accuracy and troubleshoot issues.  This tool assume that the source being compared against is local host.  For these graphs to be meaningful, localhost must point to a reliable and accurate time source,such as a GPS device.  Type "help Create-MonitorCharts.ps1 -full" for guidance.
* *Collect-W32TimeData.ps1* - Collects data using W32Time /RDTSC switch between a system under test and another machine who's clock you can use as a reference.
* *Create-TimeChart.ps1* - Produces a PNG chart from data generated using Collect-W32TimeData
* *OsTimeSampler* - A utility that is useful for measuring the performance between a Hyper-V host and it's guests.  The tool is run ith both host and guest, for instance OsTimeSampler 1000 500 > Guest1.out.  This runs for 500 samples, each 1 second apart.  The same command is run on the host.  The results...

## How to install the tools

Once you built the entire project:
1. Copy the tools and scripts directory to a single directory
1. Add that location to your path
1. Optionally you can add the scripts directly from your github local version to your path.

## How to use the tools to observe accuracy

1. The NtpMonitor services allows you to monitor many machines.  By adding registry entries the service immediately sends Ntp messages to the list of servers and records the information in log files.  At any time, you can use Create-MonitorCharts to generate charts which display a the offset Delta from a reference and RTT over time.  Using this information you can analyze the accuracy and understand if network issues are introducing jitter.  This option is good for long term monitoring.

Example Usage:

After adding a server and a reference clock (perhaps your GPS time appliance), you wait for a few hours for data to collect.  First create a new directory for your data.  After setting up the paths to the powershell tools and GNUPlot, you run "Create-MonitorCharts mySUT myGPSDevice c:\myLogData\logs".  The data will be processed for the last days worth of data, and results output describing the accuracy of various percentiles and a graph in the GraphData directory.  Old data will be backed up.


2. If you don't want to install the service, or simply want to measure a single machine, you can generate data using W32Time.  The Collect-W32TimeData simplifies the collection, which you can then use Create-TimeChart to create a chart and summary information.  This method is good to run shorter term tests, though you can set the number of samples to long periods.  

Example Usage:

First you must collect the data for both the system you want to analyze and a reference, (again your GPS time appliance).  Use "Collect-W32TimeData mySUT myGPSDevice 500", to automatically invoke w32tm as powershell jobs for both the SUT and reference clock.  This examples collects 500 samples, once every  second.  Once the data is collected, you can generate a chart and summary data by running "Create-TimeChart mySUT myGPSDevice"

3. You can also observe the accuracy between the host and guest more directly by using the OsTimeSampler tool.  It uses the TSC, which is very accurate with short time frames, to bound the time measurement samples.  By supply the delta, using the FILLINHERETOOL, and the resulting offset between the host and guest is calculated.  This can be used in addition to the tools above, which removes virtualization noise that NTP pings can't avoid as they traverse the network stack.