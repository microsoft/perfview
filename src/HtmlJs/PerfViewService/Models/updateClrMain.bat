.\robocopy /mir . \\clrmain\tools\managed\etw /W:1 /R:1 /xf *.sdf *.pch *.etl *.etl.zip *.gcdump /xd obj ipch debug testData testResults ETWClrProfiler.pch
xcopy /y /s TraceParserGen\bin \\clrmain\tools\managed\etw\TraceParserGen\bin 
xcopy /y /s ..\etw-roxel\EventSource\Public\src\eventRegister \\clrmain\tools\managed\etw\eventRegister\
