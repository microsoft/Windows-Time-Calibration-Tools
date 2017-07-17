# Make sure supporting tools are available in the path
if ((Get-Command gnuplot.exe -ErrorAction SilentlyContinue) -eq $null)
{
    echo "Gnuplot.exe not in path or not installed.  You can install from http://gnuplot.info/."
    return $false
}
if ((Get-Command MedianFilter.exe -ErrorAction SilentlyContinue) -eq $null)
{
    echo "MedianFilter.exe not in path or not installed. You can install from https://github.com/Microsoft/Windows-Time-Calibration-Tools."
    return $false
}
if ((Get-Command TimeSampleCorrelation.exe -ErrorAction SilentlyContinue) -eq $null)
{
    echo "TimeSampleCorrelation.exe not in path or not installed.  You can install from https://github.com/Microsoft/Windows-Time-Calibration-Tools."
    return $false
}

#If the we are in the same directory as the executble, warn the user
$l = Get-Location
$curpath = $l.Path + "\\" + (split-path -leaf $MyInvocation.MyCommand.Definition)
$result = dir $curpath -ErrorAction SilentlyContinue
if($result -ne $null)
{
    $EnvPathStr = $ENV:Path
    echo "These scripts create temporary files and directories.  Don't run from you scripts directory.  Instead create a new path.  To access the scripts, add them to your path."
    return $false
}

return $true