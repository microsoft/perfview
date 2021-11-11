@cls
@echo.*************************************************************
@echo.This script simply calls msbuild to build PerfView.exe
@echo.*************************************************************
@echo.
msbuild /restore /m /p:Configuration=Release %*
@if '%ERRORLEVEL%' == '0' (
    echo.
    echo.
    echo.*************************************************************
    echo.               The build was successful!
    echo.The output should be in src\bin\Release\PerfView.exe 
    echo.This is the only file needed to deploy the program.
    echo.*************************************************************
)
