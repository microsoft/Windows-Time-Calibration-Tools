Param(
   [Parameter(Mandatory=$True,Position=1)]
   [string]$f1,

   [Parameter(Mandatory=$True, Position=2)]
   [string]$f2
)

$t1h = ConvertFrom-csv (type $f1 -head 5 | select -last 1) -header a,b,c,d
$t1t = ConvertFrom-csv (type $f1 -tail 1) -header a,b,c,d
$t2t = ConvertFrom-csv (type $f2 -tail 1) -header a,b,c,d
$t2h = ConvertFrom-csv (type $f2 -head 5 | select -last 1 ) -header a,b,c,d

$sut_beg = [DateTime]::FromFileTime($t1h.c)
$sut_end = [DateTime]::FromFileTime($t2h.c)
$ref_beg = [DateTime]::FromFileTime($t2t.c)
$ref_end = [DateTime]::FromFileTime($t1t.c)

echo ("Ref beg: " + $ref_beg.ToString("u"))
echo ("  Sut beg: " + $sut_beg.ToString("u"))
echo ("  Sut end: " + $sut_beg.ToString("u"))
echo ("Ref end: " + $ref_end.ToString("u"))
