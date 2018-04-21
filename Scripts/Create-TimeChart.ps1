<#
 
.SYNOPSIS
Generates a chart based on a data from collected with Collect-TimeData.ps1 or Collect-W32TimeData.ps1.
 
.DESCRIPTION
Using data from w32tm or OsTimeSampler between a source and reference, the data is filtered and charted.  A summary is also printed for 3 percentiles.  The data can be collected manually using w32tm and OSTimeSampler, or the powershell scripts Collect-W32TimeData.ps1 and Collect-TimeData.ps1 can help automate the process.

This should be run from a 'observation' system.  The observation system will monitor a System Under Test (the device to be measured for accuracy) and a reference system.

Requirements:
    - Reference and SUT must respond to NTP Requests - For Windows, this can be done by enabling the NtpServer time provider

    - gnuplot
        - used for plotting the data and must be installed on the observation system
        - gnuplot must be in the path

    - Additional components from this project are required.  
        - Source code can be used to build the latest version of the components.
        - These additional components must also be on the path

    - Additional requirements are validated using the Test-WTCTDepedencies script

 
.EXAMPLE
Create-W32TimeChart.ps1 -Name Source -ReferenceClock Reference

Using data from Collect-W32TimeData, uses data from files Source.out and Reference.out.

.PARAMETER Name
Name of the System Under Test (SUT) that you will compare to a reference.

.PARAMETER ReferenceClock
Reference system that represents the clock to compare against. 

.LINK
https://github.com/Microsoft/Windows-Time-Calibration-Tools
 
#>

Param (
    [Parameter(Mandatory=$True,Position=1)]
    [string]$Name,

    [Parameter(Mandatory=$True,Position=2)]
    [string]$ReferenceClock,

    [Parameter(Mandatory=$False,Position=3)]
    [string]$Key,
	
    [Parameter(Mandatory=$False,Position=4)]
    [string]$FileGUID = "",

    [Parameter(Mandatory=$False)]
    [Decimal]$TSCOffset = 0,

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
    
    'set datafile separator comma'    | Out-file $outfile -Encoding ascii
    'set terminal png size 1024,768 ' | Out-file $outfile -Encoding ascii -Append
    
    'set grid '       | Out-file $outfile -Encoding ascii -Append
    'set xdata time ' | Out-file $outfile -Encoding ascii -Append

    'set timefmt "%Y-%m-%d %H:%M:%S"' | Out-file $outfile -Encoding ascii -Append

    'set format x "%m %d %T"' | Out-file $outfile -Encoding ascii -Append
    'set xtics rotate by -45' | Out-file $outfile -Encoding ascii -Append

    'set linetype 1 lc rgb "blue" lw 1 pt 0'     | Out-file $outfile -Encoding ascii -Append
    'set linetype 2 lc rgb "dark-red" lw 2 pt 0' | Out-file $outfile -Encoding ascii -Append

    'set tmargin 5'             | Out-file $outfile -Encoding ascii -Append
}

$r = Test-WTCTDepedencies.ps1

if ($r -ne $True)
{
    $r[0]
    Write-Output "Setup requirements not met.  Please view the Readme.MD on https://github.com/Microsoft/Windows-Time-Calibration-Tools for more info."
    return $False
}

# Setup directories
$GraphDataDir = ".\GraphData"
$GraphDataBackupDir = ".\GraphData\backup\"

#$Server = $Name
$WorkingDataDir = "."
$ChartTitle = $Name

if (-not (test-path $WorkingDataDir))  { md $WorkingDataDir }
if (-not (test-path $GraphDataDir))    { md $GraphDataDir }
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

    # Validation check of SUT machines files.
    if (-not (test-path $ServerOut_IP))  
    {
        Write-Output ("Missing .out data file(s) for System Under Test " + $Name) 
        return $false
    }

    # $ServerOut_IP = $ServerWorkingDir + $Name + "_" + $Key + ".out"
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
    # $localhostfile = $WorkingDataDir + "\localhost_" + $FileGUID + ".out"

    if($FileGUID -ne "")
    {
        $localhostfile = $WorkingDataDir + "\" + $ReferenceClock + "_" + $FileGUID + ".out";
    }
    else
    {
        $localhostfile = $WorkingDataDir + "\" + $ReferenceClock + ".out";
    }

    # Validation check of reference clock machine..
    if (-not (test-path $localhostfile))  
    {
        Write-Output ("Missing .out data file(s) for Reference clock " + $ReferenceClock) 
        return $false
    }


    #Created DIF file between localhost and entry using TimeSampleCorrelation tool 
    DebugPrint("TimeSampleCorrelation DIF: " + $ServerOut_IP +  " " + $localhostfile + "  = " + $ServerDif_IP)

    TimeSampleCorrelation.exe $ServerOut_IP $localhostfile $TSCOffset 0 | MedianFilter.exe 2 60 | MedianFilter.exe 3 60 |
        Out-File $ServerDif_IP -Encoding ascii -Append 

    if((Get-ChildItem $ServerDif_IP).Length -gt 0)
    {
        DebugPrint("Plotting " + $ServerDif_IP)
                    
        (type $ServerDif_IP | ConvertFrom-Csv -header time,a1,a2,a3,a4) | foreach {([datetime]($_.time)).ToString("u") + "," + $_.a3 + "," + $_.a4} | out-file $ServerPlot_IP -Encoding ascii -Append

        CreateGP($PlotGP_IP)
    
        $setoutput_cmd = "set output '" + $ServerPng_IP + "'"
        echo $setoutput_cmd | Out-file $PlotGP_IP -Encoding ascii -Append

        $settitle = "set title '" + $Server + " " + $ChartTitle + " " + $_.Name + "' font 'Courier Bold, 35'"
        echo $settitle | Out-file $PlotGP_IP -Encoding ascii -Append

        # Change Y scale if provided
        $major = 10000
        $minor = 1000
        $range = 10000.0

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
        
        echo 'set ytics add ("+1000us" 1000) add ("-1000us" -1000)' | Out-file $PlotGP_IP -Encoding ascii -Append


        echo ('plot "' + $ServerPlotGNU_IP + '" using 1:3 title "RTT" with lines, "' + $ServerPlotGNU_IP + '" using 1:2 title "UTC delta" with lines') | Out-File $PlotGP_IP -Append -Encoding ascii

        # Plot using Gnuplot, open source plotting project
        & gnuplot.exe $PlotGP_IP

        Show-Percentiles.ps1 $ServerDif_IP
    }