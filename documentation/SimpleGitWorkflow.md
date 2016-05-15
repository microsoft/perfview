#Setting Up *Without* a Fork with Visual Studio 2015 

See also [Setting up a Repo in VS 2015](SettingUpRepoInVS2015.md). 

Here we describe how to use Visual Studio 2015 to set a local build of the GitHub project https://github.com/Microsoft/perfview.   
Because this project is open source, anyone can read it so this works even if you don't have a GitHub account, but
of course you will only have read-only access.   

	1. Open Visual Studio and select the View -> Team Explorer menu item
	2. This brings up the Team Explorer pane.  At the top of the display is a bold word (probably Home) which 
	   indicates which Team Explorer view is being displayed if you left click 
       on this bolded item you will get a menu of possible displays.   Select Projects -> Manage Connections
	3. At the bottom of this display you should see a section called 'Local GIT Repositories'.  and under this a
	   set of hyperlinks.   Click on the 'Clone' hyperlink.
	4. It will bring up a dialog box where you enter two pieces of information 
	   a. The name of the source to clone.  Enter the GitHub URL (in our example https://github.com/Microsoft/perfview)
	   b. The name of the local directory to place the clone (The default is probably OK, but you can decide place it somewhere else)
	5. Click the Clone button. 
	
At this point Visual Studio will create a local clone all the files in a project in the directory you specified.  This represents
the final state of all files in the default branch (probably 'master').   It also clones repository (thus the history for all time)
and places it in a hidden .git directory in the directory you specified.   It is this cloned local repository where most GIT 
commands you do operate. 

Once the clone is complete, you will see the new local repository added to the list under 'Local Repositories'.   
You can see which one is 'active' because it is bold-faced.   This is the repository that all other VS GIT commands will
operate on.   Double click on the new entry (perfView in this example) to make it the active GIT repos.

This will bring you to the 'Home' display for a local GIT repository.   YOu will see buttons for 'changes', 'branches' 'sync', etc. 
At the bottom you will see a list of .SLN files.  There are all the Visual Studio solution files that where found in the clone
of the repository.   This makes it convenient to open VS to do a build.  In the case of PerfView, you want the PerfView.sln file.   
After opening the solution file you can start building (Build -> Build Solution or Ctrl-Shift-B).


