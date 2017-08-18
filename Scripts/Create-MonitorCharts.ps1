<#
 
.SYNOPSIS
Generates a set of charts based on NTP data collected using the Windows Time Calibration Tools.
 
.DESCRIPTION
Using Microsoft Calibration Tools and GNUPlot, this script generates a set of charts based on NTP data collected using the Windows Time Calibration Tools.
 
.EXAMPLE
Create-MonitorCharts.ps1 -ServerList time.windows.com -DataLocation c:\NtpMonitoringServiceLogs

Uses a single Time Server target and creates a graph of the time delta and time offset using records produced from NtpMonitoringService. The data collected spans the last day. 

.EXAMPLE
Create-MonitorCharts.ps1 Files.txt -DataLocation c:\NtpMonitoringServiceLogs 2

Uses a list form a text file, and createe graphs of the time delta and time offset using records produced from NtpMonitoringService. The data collected spans the last 2 days. 

.EXAMPLE
Create-MonitorCharts.ps1 Files.txt c:\NtpMonitoringServiceLogs 2 10

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
   [string]$ServerList,

   [Parameter(Mandatory=$True, Position=2)]
   [string]$ReferenceClock = "localhost",

   [Parameter(Mandatory=$True,Position=3)]
   [string]$DataLocation,

   [Parameter(Mandatory=$False, Position=4)]
   [int]$Days = 1,

   [Parameter(Mandatory=$False, Position=5)]
   [int]$HoursToDo,

   [Parameter(Mandatory=$False, Position=6)]
   [string]$ErrLog,

   [Parameter(Mandatory=$False)]
   [string]$StartTime = "",

   [Parameter(Mandatory=$False)]
   [string]$StopTime,

   [Parameter(Mandatory=$False)]
   [bool]$ShowWork,

   [Parameter(Mandatory=$False)]
   [int]$ScaleFactor
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

function SimplfyIP
{
    Param ([string] $s)


}

$r = Test-WTCTDepedencies.ps1
if($r -ne $True)
{
    echo $r[0]
    echo "Setup reqruiemtns not met.  Please view the Readme.MD on https://github.com/Microsoft/Windows-Time-Calibration-Tools for more info."
    return $False
}

# Setup directories
$GraphDataDir = ".\GraphData"
$GraphDataBackupDir = ".\GraphData\backup\"
$WorkingDataDir = ".\WorkingData"
$Plot

if (-not (test-path $WorkingDataDir))  { md $WorkingDataDir }
if (-not (test-path $GraphDataDir))  { md $GraphDataDir }
if (-not (test-path $GraphDataBackupDir))  { md $GraphDataBackupDir }

if($ErrLog -eq "")
{
    $ErrorLog = ".\err.log"
}
else
{
    $ErrorLog = $ErrLog
}

echo ("#############################") | Out-File $ErrorLog -Append
echo ("Starting " + (get-date)) | Out-File $ErrorLog -Append

# build list from input file, or a single server specified at the command line.
$slist = [System.Array]@()
if(Test-Path $ServerList)
{
    # Save old data
    $BackupDir = $GraphDataBackupDir + (date).DayOfYear + "-" + (date).Hour + "-" + (date).Minute
    echo ("Backing up old data to  " + $BackupDir) | Out-File $ErrorLog -Append
    md $BackupDir > $null
    move ($GraphDataDir + "\*.png") $BackupDir

    #flag to move data when we are done.  We only do this when using an input file.
    $InputFile = $TRUE

    DebugPrint("Using servers from file: " + $ServerList)
    $data = Get-Content $ServerList
    foreach ($line in $data)
    {
        [System.Array]$slist += $line
    } 
}
else
{
    #flag so we don't move data when we are done, since this is only one server and not a list.
    $InputFile = $FALSE
    [System.Array]$slist += $ServerList
}

DebugPrint("List of Severs")
DebugPrint($slist)

if($StartTime -eq "")
{
    # Get the files for the last X days.
    del ($WorkingDataDir + "\*.csv")
    
    $NumFiles = 24*$Days

    echo ("Plotting " + $Days + " days which ammounts to " + $NumFiles + " total CSV files") | Out-File $ErrorLog -Append 
    if  ($HoursToDo -gt 0)
    {
        $b = Get-ChildItem ($DataLocation + "\*.csv") | sort LastWriteTime | select -last $NumFiles | select -last $HoursToDo
        echo ("referenceing " + $HoursToDo + " days which reduces to " + $b.Count + " total CSV files") | Out-File $ErrorLog -Append
    }
    else
    {
        $b = Get-ChildItem ($DataLocation + "\*.csv") | sort LastWriteTime | select -last $NumFiles
    }
}
else
{
    # Support a time range
    if($StopTime -eq "")
    {
        echo "If you supply a Start time, you need to also supply a Stop time."
        exit
    }
    else
    {
        $starttm = [datetime]::ParseExact($StartTime,'dd-MM-yyyy HH:mm:ss',$null)
        $stoptm = [datetime]::ParseExact($StopTime,'dd-MM-yyyy HH:mm:ss',$null)

        if($starttm -and $stoptm)
        {
            echo ("Getting data from " + $starttm.ToString() + " to " + $stoptm.ToString())
            $b = Get-ChildItem ($DataLocation + "\*.csv") | Where-Object { $_.LastWriteTime -ge $starttm.ToString() -and $_.LastWriteTime -le $stoptm.ToString() }
        }
        else
        {
            echo "Could not convert times"
            exit
        }


    }
}

DebugPrint("-----------------------------------")
DebugPrint("List of Files:")
DebugPrint($b | foreach { $b.FullName } )

echo ("Collecting Data") | Out-File $ErrorLog -Append
#Figure out how many different GUIDs there are as data can't be processed over multiple GUIDs
$UniqueDataSets = [System.Array]@()
foreach ($n in $b)
{
    $guid = $n.Name.Substring(1,35)
    if( -NOT ($UniqueDataSets -contains $guid))
    {
        $UniqueDataSets += $guid
    }
}

DebugPrint("-----------------------------------")
DebugPrint("Unique data sets:")
DebugPrint($UniqueDataSets)

DebugPrint("-----------------------------------")
DebugPrint("Creating Reference data files for " + $ReferenceClock)
if (Test-Path  ($WorkingDataDir + "\localhost*.*")) { del ($WorkingDataDir + "\localhost*.*") }

foreach($fl in $b)
{
    $FileGUID = $fl.Name.Substring(1,35)
    #$localhostfile = $WorkingDataDir + "\localhost_" + $FileGUID + ".out"
    $localhostfile = $WorkingDataDir + "\" + $ReferenceClock + "_" + $FileGUID + ".out"
    select-string $ReferenceClock $fl.FullName | select-string -NotMatch TSC_START | foreach {$_.Line} | out-file $localhostfile -Encoding ascii -Append
    $lhf = (Get-ChildItem $localhostfile)
    DebugPrint($lhf.Name + " Size = " + $lhf.Length)
}

foreach($Server in $slist)
{
    DebugPrint("-----------------------------------")
    if ($Server.Contains("localhost")) {
        echo ("Skipping data for " + $Server) | Out-File $ErrorLog -Append
    } else {
        echo ("Processing data for " + $Server) | Out-File $ErrorLog -Append
        DebugPrint("Processing data for " + $Server)

        $ServerWorkingDir = $WorkingDataDir + "\" + $Server
        $ServerGrpahDir = $GraphDataDir + "\" + $Server

        $ServerDif = $WorkingDataDir + "\" + $Server + ".dif"
        $ServerPlot = $WorkingDataDir + "\"  + $Server + "_plot.dif"
        $ServerPlotGNU = $WorkingDataDir.Replace("\", "\\") + "\\" + $Server + "_plot.dif"
        $ServerPng = $GraphDataDir + "\" + $Server + ".png"
        $PlotGP = $WorkingDataDir + "\" + $Server + ".gp"
    
        #$ServerIP = $Server #+ "(" + $addr + ")"

        $s =  "Processing " + $Server + " for last " + $Days + " day(s)"
        echo $s | Out-File $ErrorLog -Append
        DebugPrint($s)

        #$localhostfile = ($ServerWorkingDir + "\localhost") + ".out" #+ $fl.Name.Substring(1,35) + ".out"
        #$ServerOut = ($WorkingDataDir + "\") + $Server #+ ".out" #+ dir gra


        # Clear out any old data for this server.
        if (Test-Path ($ServerWorkingDir + "*.*") ) { del ($ServerWorkingDir + "*.*") }

        $AllGroups = @()

        $b | foreach {
            $FileGUID = $fl.Name.Substring(1,35)
            DebugPrint("====> " + $fl.FullName + " searching for " + $Server)
            $GroupedIP = select-string $Server $_.FullName | select-string -NotMatch TSC_START | foreach {
                 $obj = (ConvertFrom-Csv $_.Line -header IP, start, end, time, delay, name, addr, resolvedname) 

                 $index = $obj.resolvedname.IndexOf(".")
                 if($index -gt 0)
                 {
                     $machineName = $obj.resolvedname.Substring(0, $obj.resolvedname.IndexOf("."))
                 }
                 else
                 {
                     $machineName = $obj.resolvedname
                 }

                 #Does the line in the CSV contain the comptuer name we are looking for.  It can be in several places.
                 if(($obj.IP -eq $Server) -or ($obj.resolvedname.StartsWith($Server,"CurrentCultureIgnoreCase") -or ($obj.name -eq $Server) )){ $obj } 
            } | Group-Object -Property IP

            $GroupedIP | ForEach-Object {
                $_ | Add-Member -Name key -Value $FileGUID -MemberType NoteProperty
                $_.Group | ForEach-Object {
                    $SimpleIP = $_.IP.Replace(":","_")
                    $ServerOut_IP = $ServerWorkingDir + $SimpleIP + "_" + $FileGUID + ".out"
                    echo ($_.IP + "," + $_.start + "," + $_.end + "," + $_.time + "," + $_.delay) | out-file $ServerOut_IP -Encoding ascii -Append
                }

                $GroupSummary = New-Object PSObject
                Add-Member -InputObject $GroupSummary -MemberType NoteProperty -Name SUTName -Value $_.Name
                Add-Member -InputObject $GroupSummary -MemberType NoteProperty -Name key -Value $FileGUID
                Add-Member -InputObject $GroupSummary -MemberType NoteProperty -Name ResolveName -Value $GroupedIP.Group[0].ResolvedName

                $AllGroups += $GroupSummary 
            }


            if($GroupedIP.Count -ne 0)
            {
                DebugPrint("Groupings for " + $Server + " for file " + $_.Name)
                DebugPrint($GroupedIP.Name)

            }
       }

       DebugPrint("All groups")
       DebugPrint($AllGroups | Select -Unique)

        #if (Test-Path $ServerDif ) { del ($ServerDif + "*.*") }

#        $GroupedIP | ForEach-Object {
         $AllGroups | Select -Unique | ForEach-Object {
            DebugPrint("Processing " + $_.SUTName + " " + $_.ResolvedName)

            #$SimpleIP = $_.IP.Replace(":","_")

            $ServerOut_IP = $ServerWorkingDir + $_.SUTName + "_" + $_.key + ".out"
            $ServerPlot_IP = $ServerWorkingDir + $_.SUTName + "_plot.dif"
            $ServerDif_IP = $ServerWorkingDir + $_.SUTName + ".dif"
            $PlotGP_IP = $ServerWorkingDir + $_.SUTName + ".gp"
            $ServerPng_IP = $ServerGrpahDir + $_.SUTName + ".png"
            $ServerPlotGNU_IP = $WorkingDataDir.Replace("\", "\\") + "\\" + $Server + $_.SUTName + "_plot.dif"
            #$localhostfile = $WorkingDataDir + "\localhost_" + $FileGUID + ".out"
            $localhostfile = $WorkingDataDir + "\" + $ReferenceClock + "_" + $FileGUID + ".out"

            #Created DIF file between localhost and entry using TimeSampleCorrelcation tool 
            DebugPrint("TimeSampleCorrelation DIF: " + $ServerOut_IP +  " " + $localhostfile + "  = " + $ServerDif_IP)
            TimeSampleCorrelation.exe $ServerOut_IP $localhostfile 0 1 | MedianFilter.exe 2 60 | MedianFilter.exe 3 60 |
             out-file $ServerDif_IP -Encoding ascii -Append 

            if((dir $ServerDif_IP).Length -gt 0)
            {
                DebugPrint("Plotting " + $ServerDif_IP)
                    
                (type $ServerDif_IP | ConvertFrom-Csv -header time,a1,a2,a3,a4) | foreach {([datetime]($_.time)).ToString("u") + "," + $_.a3 + "," + $_.a4} | out-file $ServerPlot_IP -Encoding ascii -Append

                CreateGP($PlotGP_IP)
    
                $setoutput_cmd = "set output '" + $ServerPng_IP + "'"
                echo $setoutput_cmd | Out-file $PlotGP_IP -Encoding ascii -Append

                $settitle = "set title '" + $Server + " " + $_.ResovledName + " " + $_.SUTName + "' font 'Courier Bold, 35'"
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

                Show-Percentiles.ps1 $ServerDif_IP

                if($InputFile)
                {
                    echo "Copying data to public share" | Out-File $ErrorLog -Append
                    copy $ServerPng_IP TimeWindowsComData
                }
            } else {
                $errinfo = "Error DIF file is zero length for " + $Server + $_.key + " .  Does the localhost file overlap?"
                DebugPrint($errinfo);
                echo $errinfo | Out-File $ErrorLog -Append
            }
        }
    }
}

echo "===================== Finished! =======================" | Out-File $ErrorLog -Append

