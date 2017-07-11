<#
 
.SYNOPSIS
Collects data from a system and a reference clock using W32tm or OsTimeSampler if localhost is specified.
 
.DESCRIPTION
Using data from w32tm or OsTimeSampler between a source and refecnce, the data is collected and saved with a .out extension appened to the system names. If the source or reference is LocalHost, OsTimeSample is used instead, which uses a method which doens't by passes the newtork stack and makes mesaurements more accurate.
 
.EXAMPLE
Collect-W32TimeData.ps1 Source Reference 500

Using data from Collect-W32TimeData, uses data from files Source.out and Reference.out.

.PARAMETER SUT
Name of the System Undert Test (SUT) that you will compare to a reference.

.PARAMETER ReferenceClockSystem
Reference system that represents the clock to compare against. 

.PARAMETER Samples
Number of Samples to take overall.  Samples occur once every second.

.LINK
https://github.com/Microsoft/Windows-Time-Calibration-Tools
 
#>

Param(
   [Parameter(Mandatory=$True,Position=1)]
   [string]$SUT,

   [Parameter(Mandatory=$True,Position=2)]
   [string]$ReferenceClockSystem,

   [Parameter(Mandatory=$False,Position=3)]
   [string]$Samples = 100
)

$SUTData = $SUT + ".out"
$ReferenceClockSystemData = $ReferenceClockSystem + ".out"

$SUTDataPre = $SUT + "_pre" + ".out"
$ReferenceClockSystemDataPre = $ReferenceClockSystem + "_pre" + ".out"

$d = get-location

if($SUT -eq "localhost")
{
    $SUTTool = "OsTimeSampler.exe 1000 " + $Samples
} else {
    $SUTTool = "w32tm /stripchart /computer:" + $SUT + " /rdtsc /period:1 /samples:" + $Samples
}

if($ReferenceClockSystem -eq "localhost")
{
    $RefTool = "OsTimeSample.exe 1000 " + $Samples
} else {
    $RefTool = "w32tm /stripchart /computer:" + $ReferenceClockSystem + " /rdtsc /period:1 /samples:" + $Samples
}

$SUT_job = [scriptblock]::Create("cd " + $d + "; & " + $SUTTool + " > " + $SUTDataPre )
$Ref_job = [scriptblock]::Create("cd " + $d + "; & " + $RefTool + " > " + $ReferenceClockSystemDataPre )

$j1 = start-job -Name j1 -ScriptBlock $SUT_job 
$j2 = start-job -Name j2 -ScriptBlock $Ref_job

echo "Collecting data..."
echo $j1.Command
echo $j2.Command

echo "Waiting for data to be collected..."
wait-job -Name j1,j2 
echo "Data collected"

#w32tm is in seconds, need to convert to microseconds if not localhost

if($SUT -ne "localhost")
{
    Convert-ColumnByScale.ps1 $SUTDataPre > $SUTData
}
else
{
    Copy $SUTDataPre $SUTData
}

if($ReferenceClockSystem -ne "localhost")
{
    Convert-ColumnByScale.ps1 $ReferenceClockSystemDataPre > $ReferenceClockSystemData
}
else
{
    Copy $ReferenceClockSystemDataPre $ReferenceClockSystemData
}

remove-job -Name j1,j2
