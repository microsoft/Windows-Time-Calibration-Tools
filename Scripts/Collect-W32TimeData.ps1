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
create-time
$SUT_job = [scriptblock]::Create("cd " + $d + "; & w32tm /stripchart /computer:" + $ReferenceClockSystem + " /rdtsc /samples:" + $Samples + " > " + $ReferenceClockSystemDataPre )
$Ref_job = [scriptblock]::Create("cd " + $d + "; & w32tm /stripchart /computer:" + $SUT +                  " /rdtsc /samples:" + $Samples + " > " + $SUTDataPre )

$j1 = start-job -Name j1 -ScriptBlock $SUT_job 
$j2 = start-job -Name j2 -ScriptBlock $Ref_job

echo "Collecting data..."
echo $j1.Command
echo $j2.Command

echo "Waiting for data to be collected..."
wait-job -Name j1,j2 
echo "Data collected"

#w32tm is in seconds, need to convert to microseconds

Convert-ColumnByScale.ps1 $SUTDataPre create> $SUTData
Convert-ColumnByScale.ps1 $ReferenceClockSystemDataPre > $ReferenceClockSystemData

remove-job -Name j1,j2
