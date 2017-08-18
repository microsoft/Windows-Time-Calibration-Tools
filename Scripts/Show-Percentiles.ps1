# From http://www.dummies.com/education/math/statistics/how-to-calculate-percentiles-in-statistics/

Param(
   [Parameter(Mandatory=$True,Position=1)]
   [string]$File
)

$obj = type $file | ConvertFrom-Csv -header time, a1, a2, a3, a4 | sort @{expression={[math]::Abs($_.a1) -as [double]} }

$percentiles = 0.68, 0.95, 0.997 

echo (dir $File).BaseName

$percentiles  | foreach {
    $pp = $obj.count * $_

    if($_ -eq [math]::Round($x_)){
        $p = [math]::Round($pp + 1)
        echo ("  The " + ($_ * 100)+ "th percentile = " + ([math]::Abs($obj[$p - 1].a1)) + "us")
    } else {
        echo (" The " + ($_ * 100) + "th percentile = " + ([math]::Abs($obj[$pp - 1].a1)) + "us")
    }
}