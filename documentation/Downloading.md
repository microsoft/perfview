# Downloading PerfView 

PerfView is a free profiling tool from Microsoft.   This page tells you how to get a copy of it for yourself.  
See the [PerfView Overview](https://github.com/Microsoft/perfview#perfview-overview) for general information
about PerfView.   

PerfView is an x64 application requiring Windows 10 or Windows Server 2016 or later
and .NET Framework 4.7.2 or later. It can collect from both x86 and x64 target processes.
Windows-provided x64 emulation may be used where available; there is no native ARM64
PerfView application build.

# PerfView Releases

The [PerfView GitHub Releases page](https://github.com/Microsoft/perfview/releases) is now the official 
way to download versions of the PerfView.
It shows the release notes and the full set of binaries for all current and past releases.  If you
care about specific bug fixes or features go there.  

## Shortcut to Download the Latest PerfView.exe

In the common case, you only need one file, PerfView.exe, to use the tool.  The most recent copy of
this file can be downloaded [directly from this link](https://aka.ms/perfview/latest).

Once you click the above link in your browser it will start downloading, the details of which vary from browser to browser.
In some cases it will prompt for more information (IE) and in others (Chrome) it may not be obvious that
you clicked on anything (look at the bottom of the pane for changes).  The result, however will be a PerfView.exe on your
local machine.

Once downloaded you you can simply double click on the downloaded EXE to launch PerfView.
While Github itself and your browser do some validation, to be extra careful you can
also explicitly validate the digital signature of the downloaded file before running it.

You can also script download PerfView.exe using curl:

```
curl -L -o perfview.exe https://aka.ms/perfview/latest
```

### Explicitly Validating PerfView's Digital Signature

Like all official Microsoft software, PerfView.exe is digitally signed
by Microsoft so you can have confidence that this software came from Microsoft
and has not been tampered with since the time it was created by Microsoft.
You can confirm this by opening the directory where you downloaded PerfView.exe and selecting
the Digital Signatures page for perfView by

* Selecting PerfView -> right click -> Properties -> Digital Signatures.

You will see a sha256 signature from the 'Microsoft Corporation'.
This confirms that software is authentic. You can also do this by running the signtool command
```
 signtool verify /pa /all  PerfView.exe
```

# Additional Release Information

The link above allows you to quickly download PerfView.exe which is what you need 95% of the time.
If you wish to get other files associated with the release (e.g. debugging symbols),
or wish to download older releases (not recommended), you can do
so by visiting the [PerfView GitHub Releases](https://github.com/Microsoft/perfview/releases).


## Microsoft Download Site
The Microsoft Download Site for PerfView has
been retired as the official download site for PerfView.  It has been replaced by
[PerfView GitHub Releases page](https://github.com/Microsoft/perfview/releases) referenced above. 

