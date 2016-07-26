
To get code signed, go to 

https://codesign.gtm.microsoft.com/

You need to run codeSignSetup\setup.exe once on the machine. 

The interface is not Very friendly.  

You will need 2 approvers (you can give more and the first two that approve unblockes the job)

Here are the approvers that I have used in the past

tlai
andrehal
dantaylo
leecow

It will ask for the kind of signing,   You want authenticode (not strong name).  


10006 Certificate
You also want the Sha256 for Win 10 (how exactly to you ask for it to be signed both ways)?


Use the name 	PerfView

And the URL 	http://www.microsoft.com/download/en/details.aspx?id=28567

You will add just the PerfVIew.exe exe to be signed.  
