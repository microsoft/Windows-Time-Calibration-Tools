Installation:

* Build the service.
* Copy the binaries to location on monitoring machine (example would be c:\ntp).
* Create the serivce:
	* sc create NtpMonitor binpath= c:\ntp\MonitoringService.exe start= auto
* Create the registry keys or alternatively, use the Example.reg as a template.
	* HKLM\System\CurrentControlSet\Services\NtpMonitor\Config
	 	* BasePath REG_SZ "C:\ntp\logs"
	 	* BasePath REG_SZ "C:\ntp\resolverlogs"
	* HKLM\System\CurrentControlSet\Services\NtpMonitor\Servers
		* NtpServer1 REG_SZ "5000"
* Create directories for logs
* Create directories for resolverlogs


