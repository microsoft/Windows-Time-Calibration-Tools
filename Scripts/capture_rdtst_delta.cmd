tracelog -start rdtsc -f hyper-v.etl -b 1024 -min 8 -max 8 -batched
tracelog -enableex rdtsc -guid #910C653D-A4EB-4719-B909-4588E3BAEC91 -matchanykw 0x0000000000002000
rem tracelog -enableex rdtsc -guid #910C653D-A4EB-4719-B909-4588E3BAEC91 -matchanykw 0xFFFFFFFFFFFFFFFF
pause
tracelog -stop rdtsc
netsh trace convert hyper-v.etl hyper-v.xml xml