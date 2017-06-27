Param(
   [Parameter(Mandatory=$True,Position=1)]
   [string]$File,

   [Parameter(Mandatory=$False,Position=2)]
   [string]$Column,

   [Parameter(Mandatory=$False,Position=3)]
   [string]$Scale,

   [Parameter(Mandatory=$False)]
   [bool]$ShowWork
)

(type $File | ConvertFrom-Csv -header a0,a1,a2,a3,a4) | foreach {
    $da3 = [double]$_.a3 * 1000000
    $da4 = [double]$_.a4 * 1000000
    echo ($_.a0 + "," + $_.a1 + "," + $_.a2 + "," + $da3 + "," + $da4)
} 
