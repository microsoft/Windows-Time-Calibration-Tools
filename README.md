# Windows-Time-Calibration-Tools
Window Tools for Calibrating Windows Time Service

The set of tools included are designed to measure and calibrate the Windows time service.  It consists of various tools, some of which support the higher level functions.  At a high level, they provide the following functions:
* *NtpMonitor Service* - Identifies machines to monitor via registry entries which then sends NTP requests.  The service uses regular NTP messages and produces logs.  The log files can be analyze and charted using the GenerateGraphs.ps1 script in the Scripts directly.
* *GenerateGraphs.ps1* - A PowerShell script which produces graphs to display the accuracy again 1ms accuracy.  This tool assume that the source being compared against is local host.  For these graphs to be meaningful, localhost must point to a reliable and accurate time source,such as a GPS device.  Type "help GenerateGraphs.ps1 -full" for guidance.
