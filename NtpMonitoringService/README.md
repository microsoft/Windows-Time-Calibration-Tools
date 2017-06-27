Installation:

* Build the service.
* Copy the binaries to location on monitoring machine (example would be c:\ntp).
* Create the service:
	* sc create NtpMonitor binpath= c:\ntp\MonitoringService.exe start= auto
* Create the registry keys or alternatively, use the Example.reg as a template.
	* HKLM\System\CurrentControlSet\Services\NtpMonitor\Config
	 	* BasePath REG_SZ "C:\ntp\logs"
	 	* BasePath REG_SZ "C:\ntp\resolverlogs"
* Create directories for logs - this is the NTP data collected by the service
* Create directories for resolverlogs - these logs help you troubleshoot why data might be missing.

Generating Data from logs:

Setup each machine you want to monitor.  Restart of service is not required.

* HKLM\System\CurrentControlSet\Services\NtpMonitor\Servers
	* NtpServer1 REG_SZ ""

To generate charts and a report based on the NtpMonitor Server Data:
* Create a directory for you work 
* Change to that directory
* Create a text file, serverlist.txt, which contains a list of servers you want to monitor.
* Create-MonitorCharts.ps1 .\serverlist.txt localhost c:\ntp\logs 7
	Generates graph for the last 7 days, default is 1 day.  Uses localhost as the reference clock.
* Review data in Graph subdirectory