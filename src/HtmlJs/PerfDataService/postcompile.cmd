set scriptDir=%~dp0
set RuntimeOutputDir=%1
set RuntimeIdentifier=%2
set nativeBinDir=%RuntimeOutputDir%\Amd64
if NOT EXIST %nativeBinDir% (
    mkdir %nativeBinDir%
)
copy /y %scriptDir%\..\..\TraceEvent\amd64\msdia140.dll %nativeBinDir%\msdia140.dll
