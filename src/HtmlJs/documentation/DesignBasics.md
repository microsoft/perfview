

#Basic Design of the HTLM/JavaScript version of PerfView.  

## Requirements

The main goal of this code is to allow users on Linux and MacOS to 
get the value of PerfView viewer, without having to transfer their
data to a windows machine.   

It is not a goal (at least initially) to deal with collect of the data
on these machines (since that is platform specific) but to provide a 
viewer for the data once it is collect (since that is platform neutral).

It is a goal to reuse as much of the existing code base as possible.   
It is expected, howevever that all the GUI part of PerfView will have
to be rewritten.   

It is also a goal to allow this version to work in two scearios

   1. Where the data file lives on the local machine and the 
      tool acts as a standard local application that reads the file
      and displays the result.

   2. Where the data file lives on another computer and you access 
      and is being served up as a HTTP REST Service and the viewer
      is simply a browser on the same or a different machine.   

## Architecture





