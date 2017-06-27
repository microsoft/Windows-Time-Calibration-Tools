<#
 
.SYNOPSIS
Generates a set of charts based on NTP data collected using the Windows Time Calibration Tools.
 
.DESCRIPTION
Using Microsoft Calibration Tools and GNUPlot, this script generates a set of charts based on NTP data collected using the Windows Time Calibration Tools.
 
.EXAMPLE
GenerateGraphs.ps1 -ServerList time.windows.com -DataLocation c:\NtpMonitoringServiceLogs

Uses a single Time Server target and creates a graph of the time delta and time offset using records produced from NtpMonitoringService. The data collected spans the last day. 

.EXAMPLE
GenerateGraphs.ps1 Files.txt -DataLocation c:\NtpMonitoringServiceLogs 2

Uses a list form a text file, and createe graphs of the time delta and time offset using records produced from NtpMonitoringService. The data collected spans the last 2 days. 

.EXAMPLE
GenerateGraphs.ps1 Files.txt c:\NtpMonitoringServiceLogs 2 10

Uses a list form a text file, and createe graphs of the time delta and time offset using records produced from NtpMonitoringService. The data collected spans the last 2 days, but only 10 log files from that point.  By default, as the NtpMonitor service is configured, each files represents an hour. 

.PARAMETER ServerList
A single Time Server target, or a text file with a list of Time Server targets separated by newlines. 

.PARAMETER DataLocation
Location of data created by the Windows Time Calibration NtpMonitoringServer tool. 

.PARAMETER ReferenceClock
Name of system which will be used as the reference clock.

.PARAMETER StartTime
The starting point data should be graphed.  Requires StopTime parameter.  The date is in the form: dd-MM-yyyy HH:mm:ss

.PARAMETER StopTime
The stopping point data should be graphed.  Request the StartTime parameter.  The date is in the form: dd-MM-yyyy HH:mm:ss


.NOTES
GraphData and WorkingData directories are created and used for the data temp working space and the final charts as .png.

Path must include Cadilibration tools, GNUPlot, and most likely this scripting tool.
 
.LINK
https://github.com/Microsoft/Windows-Time-Calibration-Tools
 
#>

# PS C:\>$T = New-JobTrigger -Weekly -At "9:00 PM" -DaysOfWeek Monday -WeeksInterval 2
# PS C:\>Register-ScheduledJob -Name "UpdateVersion" -FilePath "\\Srv01\Scripts\UpdateVersion.ps1" -Trigger $T -ScheduledJobOption $O

Param(
   [Parameter(Mandatory=$True,Position=1)]
   [string]$Name,

   [Parameter(Mandatory=$True,Position=2)]
   [string]$ReferenceClock,

   [Parameter(Mandatory=$True,Position=3)]
   [string]$ChartTitle,

   [Parameter(Mandatory=$False,Position=4)]
   [string]$Key,
	
   [Parameter(Mandatory=$False,Position=5)]
   [string]$FileGUID = "",

   #[Parameter(Mandatory=$False,Position=5)]
   #[string]$WorkingDataDir = ".",

   [Parameter(Mandatory=$False)]
   [string]$Server,

   [Parameter(Mandatory=$False)]
   [int]$ScaleFactor,

   [Parameter(Mandatory=$False)]
   [bool]$ShowWork
)

function DebugPrint
{
    Param ( $s)
    
    if($ShowWork)
    {
        Write-Host $s
    }
}

function CreateGP
{
    Param ([string] $outfile)
    
    echo "set datafile separator comma" | Out-file $outfile -Encoding ascii
    echo "set terminal png size 1024,768 " | Out-file $outfile -Encoding ascii -Append
    echo "set grid " | Out-file $outfile -Encoding ascii -Append
    echo "set xdata time " | Out-file $outfile -Encoding ascii -Append
    echo 'set timefmt "%Y-%m-%d %H:%M:%S"' | Out-file $outfile -Encoding ascii -Append
    echo 'set format x "%m %d %T"' | Out-file $outfile -Encoding ascii -Append
    echo 'set xtics rotate by -45' | Out-file $outfile -Encoding ascii -Append
    echo 'set linetype 1 lc rgb "cyan" lw 1 pt 0' | Out-file $outfile -Encoding ascii -Append
    echo 'set linetype 2 lc rgb "dark-red" lw 2 pt 0' | Out-file $outfile -Encoding ascii -Append
    echo 'set tmargin 5' | Out-file $outfile -Encoding ascii -Append
}

# Setup directories
$GraphDataDir = ".\GraphData"
$GraphDataBackupDir = ".\GraphData\backup\"
#$Server = $Name
$WorkingDataDir = "."

if (-not (test-path $WorkingDataDir))  { md $WorkingDataDir }
if (-not (test-path $GraphDataDir))  { md $GraphDataDir }
if (-not (test-path $GraphDataBackupDir))  { md $GraphDataBackupDir }

        $ServerWorkingDir = $WorkingDataDir + "\" + $Server
        $ServerGrpahDir = $GraphDataDir + "\" + $Server

    if($Key -ne "")
    {
        $ServerOut_IP = $ServerWorkingDir + $Name + "_" + $Key + ".out"
    }
    else
    {
        $ServerOut_IP = $ServerWorkingDir + $Name + ".out"
    }

    #$ServerOut_IP = $ServerWorkingDir + $Name + "_" + $Key + ".out"
    $ServerPlot_IP = $ServerWorkingDir + $Name + "_plot.dif"
    $ServerDif_IP = $ServerWorkingDir + $Name + ".dif"
    $PlotGP_IP = $ServerWorkingDir + $Name + ".gp"
    $ServerPng_IP = $ServerGrpahDir + $Name + ".png"

    if($Server -ne "")
    {
        $ServerPlotGNU_IP = $WorkingDataDir.Replace("\", "\\") + "\\" + $Server + $Name + "_plot.dif"
    }
    else
    {
        $ServerPlotGNU_IP = $WorkingDataDir.Replace("\", "\\") + "\\" + $Name + "_plot.dif"
    }
    #$localhostfile = $WorkingDataDir + "\localhost_" + $FileGUID + ".out"

    if($FileGUID -ne "")
    {
        $localhostfile = $WorkingDataDir + "\" + $ReferenceClock + "_" + $FileGUID + ".out";
    }
    else
    {
        $localhostfile = $WorkingDataDir + "\" + $ReferenceClock + ".out";
    }

    #Created DIF file between localhost and entry using TimeSampleCorrelcation tool 
    DebugPrint("TimeSampleCorrelation DIF: " + $ServerOut_IP +  " " + $localhostfile + "  = " + $ServerDif_IP)
    .\TimeSampleCorrelation.exe $ServerOut_IP $localhostfile 0 0 | MedianFilter.exe 2 60 | MedianFilter.exe 3 60 |
        out-file $ServerDif_IP -Encoding ascii -Append 

    if((dir $ServerDif_IP).Length -gt 0)
    {
        DebugPrint("Plotting " + $ServerDif_IP)
                    
        (type $ServerDif_IP | ConvertFrom-Csv -header time,a1,a2,a3,a4) | foreach {([datetime]($_.time)).ToString("u") + "," + $_.a3 + "," + $_.a4} | out-file $ServerPlot_IP -Encoding ascii -Append

        CreateGP($PlotGP_IP)
    
        $setoutput_cmd = "set output '" + $ServerPng_IP + "'"
        echo $setoutput_cmd | Out-file $PlotGP_IP -Encoding ascii -Append

        $settitle = "set title '" + $Server + " " + $ChartTitle + " " + $_.Name + "' font 'Courier Bold, 35'"
        echo $settitle | Out-file $PlotGP_IP -Encoding ascii -Append

        # Change Y scale if provided
        $major = 15000
        $minor = 5000
        $range = 15000.0

        if($ScaleFactor -gt 0)
        {
            $major = $ScaleFactor * $major
            $minor = $ScaleFactor * $minor
            $range = $ScaleFactor * $range

            if($ShowWork)
            {
                echo ("Scaling by " + $ScaleFactor)
            }
        }

        $ytics = "set ytics nomirror axis scale " + $major + " " + $minor
        echo $ytics | Out-file $PlotGP_IP -Encoding ascii -Append
        $yrange = "set yrange [" + ($range * -1.0) + ":" + ($range) + "]"
        echo $yrange | Out-file $PlotGP_IP -Encoding ascii -Append
        echo 'set ytics add ("+1ms" 1000) add ("-1ms" -1000)' | Out-file $PlotGP_IP -Encoding ascii -Append


        echo ('plot "' + $ServerPlotGNU_IP + '" using 1:3 title "RTT" with lines, "' + $ServerPlotGNU_IP + '" using 1:2 title "UTC delta" with lines') | Out-File $PlotGP_IP -Append -Encoding ascii

        # Plot using Gnuplot, open source plotting project
        & gnuplot.exe $PlotGP_IP
    }