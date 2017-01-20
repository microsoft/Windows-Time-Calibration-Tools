#gci ..\logs\*.csv | sort LastWriteTime | select -last 10
Param(
   [Parameter(Mandatory=$True,Position=1)]
   [string]$ServerList,
	
   [Parameter(Mandatory=$True, Position=2)]
   [int]$Days
)

# Save old data
if (-not (test-path .\TimeWindowsComData\backup)) { md .\TimeWindowsComData\backup } else { del .\TimeWindowsComData\backup }
move .\TimeWindowsComData\*.* .\TimeWindowsComData\backup

# build list from input file, or a single server specified at the command line.
$slist = [System.Array]@()
if(Test-Path $ServerList)
{
    $data = Get-Content $ServerList
    foreach ($line in $data)
    {
        [System.Array]$slist += $line
    } 
}
else
{
    [System.Array]$slist += $ServerList
}

# Get the files for the last X days.
del .\WorkingLogSet\*.csv
if (-not (test-path .\WorkingLogSet))  { md .\WorkingLogSet }
#$b = get-childitem ..\logs\*.csv | sort LastWriteTime | where-object {$_.LastWriteTime -gt (get-date).AddDays(-$Days)} 
$NumFiles = 24*$Days

echo ("Plotting " + $Days + " days which ammounts to " + $NumFiles + " total CSV files")
$b = gci ..\logs\*.csv | sort LastWriteTime | select -last $NumFiles

#Figure out how many different GUIDs there are as data can't be processed over multiple GUIDs
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


if (Test-Path ".\WorkingLogSet\localhost*.out") { del .\WorkingLogSet\localhost*.out }
foreach($fl in $b)
{
    $localhostfile = ".\WorkingLogSet\localhost" + $fl.Name.Substring(1,35) + ".out"
    select-string localhost $fl.FullName | select-string -NotMatch TSC_START | foreach {$_.Line} | out-file $localhostfile -Encoding ascii -Append
}

foreach($Server in $slist)
{
    $ServerDif = ".\WorkingLogSet\" + $Server + ".dif"
    $ServerPlot = ".\WorkingLogSet\" + $Server + "_plot.dif"
    $ServerPlotGNU = ".\\WorkingLogSet\\" + $Server + "_plot.dif"
    $ServerPng = ".\WorkingLogSet\" + $Server + ".png"

    $ipa = Resolve-DnsName $Server
    if ($ipa)
    {
        $addr = $ipa.IP4Address
    }
    else
    {
        $addr = "NA";
    }

    $ServerIP = $Server + "(" + $addr + ")"

    $s =  "Processing " + $Server + " (" + $addr + ") for last " + $Days + " day(s)"
    echo $s

    $localhostfile = ".\WorkingLogSet\localhost" + $fl.Name.Substring(1,35) + ".out"
    $ServerOut = ".\WorkingLogSet\" + $Server + $fl.Name.Substring(1,35) + ".out"
    if (Test-Path $ServerOut ) { del $ServerOut }
    if (Test-Path $ServerDif ) { del $ServerDif }
    $b | foreach {select-string $Server $_.FullName | select-string -NotMatch TSC_START | foreach {$_.Line} | out-file $ServerOut -Encoding ascii -Append}
    ..\TimeSampleCorrelation.exe $ServerOut $localhostfile 0 1 | ..\MedianFilter.exe 2 60 | ..\MedianFilter.exe 3 60 | out-file $ServerDif -Encoding ascii -Append 

    if (Test-Path $ServerPlot ) { del $ServerPlot }
    (type $ServerDif | ConvertFrom-Csv -header time,a1,a2) | foreach {([datetime]($_.time)).ToString("u") + "," + $_.a1 + "," + $_.a2} | out-file $ServerPlot -Encoding ascii -Append

    copy .\plotsetup_base.gp plotsetup.gp
    $setoutput_cmd = "set output '" + $ServerPng + "'"

    echo $setoutput_cmd | Out-file .\plotsetup.gp -Encoding ascii -Append

    echo ('plot "' + $ServerPlotGNU + '" using 1:3 title "' + $ServerIP + ' RTT" with lines, "" using 1:2 title "' + $ServerIP + ' UTC delta" with lines') | Out-File .\plotsetup.gp -Append -Encoding ascii

    .\Plot.ps1

    copy $ServerPng TimeWindowsComData
}