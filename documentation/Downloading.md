# Downloading PerfView 

The [PerfView GitHub Releases page](https://github.com/Microsoft/perfview/releases)
is now the official way to download versions of the PerfView.  
It shows the release notes and the full set of binaries for all current and past releases.

## Shortcut to Download the Latest PerfView.exe

The latest version of PerfView can be downloaded with the following link:

* [Download Version 2.02 of PerfView.exe](https://github.com/Microsoft/perfview/releases/download/P2.0.2/PerfView.exe)


### Explicitly Validating PerfView's Digital Signature

Like all official Microsoft software, PerfView.exe is digitally signed
by Microsoft so you can have confidence that this software came from Microsoft
and has not been tampered with.  You can confirm this by opening the directory
where you downloaded PerfView.exe, and select the Digital Signatures page for perfView by

* Selecting PerfView -> right click -> Properties -> Digital Signatures.  

You will see a sha1 and sha256 signature from the 'Microsoft Corporation'.
This confirms that software is authentic. You can also do this by running the signtool command 
```
 signtool verify /pa /all  PerfView.exe
```

# Additional Release Information

The link above allows you to quickly download PerfView.exe which is what you need 95% of the time.
If you wish to get other files associated with the release (e.g. the 64 bit version, or
the debugging symbols), or wish to download older releases (not recommended), you can do
so by visiting the [PerfView GitHub Releases](https://github.com/Microsoft/perfview/releases).


## Microsoft Download Site
The [Microsoft Download Site for PerfView](https://www.microsoft.com/en-us/download/details.aspx?id=28567) has
been retired as the official download site for PerfView.  It has been replaced by
[PerfView GitHub Releases page](https://github.com/Microsoft/perfview/releases) referenced above. 


