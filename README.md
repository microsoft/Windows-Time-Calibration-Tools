# Windows-Time-Calibration-Tools
Window Tools for Calibrating Windows Time Service

The set of tools included are designed to measure and calibrate the Windows time (or for that matter any time) service.  It consists of various tools, some of which support the higher level functions.  At a high level, they provide the following functions:
* *NtpMonitor Service* - Identifies machines to monitor via registry entries which then sends NTP requests.  The service uses regular NTP messages and produces logs.  The log files can be analyze and charted using the GenerateGraphs.ps1 script in the Scripts directly.
* *Create-MonitorCharts.ps1* - A PowerShell script which produces graphs to display the accuracy again 1ms accuracy.  This tool assume that the source being compared against is local host.  For these graphs to be meaningful, localhost must point to a reliable and accurate time source,such as a GPS device.  Type "help Create-MonitorCharts.ps1 -full" for guidance.
* *Collect-W32TimeData.ps1* - Collects data using W32Time /RDTSC switch between a system under test and another machine who's clock you can use as a reference.
* *Create-TimeChart.ps1* - Produces a PNG chart from data generated using Collect-W32TimeData

How to use the tools to observe accuracy:

1. The NtpMonitor services allows you to monitor many machines.  By adding registry entries the service immediately sends Ntp messages to the list of servers and records the information in log files.  At any time, you can use Create-MonitorCharts to generate charts which display a the offset Delta from a reference and RTT over time.  Using this information you can analyze the accuracy and understand if network issues are introducing jitter.  This option is good for long term monitoring.

Example Usage:

After adding a server and a reference clock (perhaps your GPS time appliance), you wait for a few hours for data to collect.  After setting up the paths to the powershell tools and GNUPlot, you run "Create-MonitorCharts mySUT myGPSDevice c:\myLogData\logs".  The data will be processed for the last days worth of data, and results output describing the accuracy of various percentiles and a graph in the GraphData directory.  Old data will be backed up.


2. If you don't want to install the service, or simply want to measure a single machine, you can generate data using W32Time.  The Collect-W32TimeData simplifies the collection, which you can then use Create-TimeChart to create a chart and summary information.  This method is good to run shorter term tests, though you can set the number of samples to long periods.  

Example Usage:

First you must collect the data for both the system you want to analyze and a reference, (again your GPS time appliance).  Use "Collect-W32TimeData mySUT myGPSDevice 500", to automatically invoke w32tm as powershell jobs for both the SUT and reference clock.  This examples collects 500 samples, once a second..  Once the data is collected, you can generate a chart and summary data by running "Create-TimeChart mySUT myGPSDevice"