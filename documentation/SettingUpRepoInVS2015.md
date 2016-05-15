#Setting up a Local GitHub repository with Visual Studio 2015. 

This section tells you how to set up to build a project that is hosted on GitHub, 
using Visual Studio 2015.   It also goes through important routine tasks like 
getting the latest changes from GitHub and submitting a pull request to the master branch.

It will show you how to do this using just Visual Studio 2015.   It will also show
how to do this with command line GIT as well.    Older version of Visual Studio as
well as other IDEs are possible, but not covered here.  

We use the PerfView project https://github.com/Microsoft/perfview as an example but 
the vast majority of this section applies to any open source GitHub hosted project.  

##Background: GitHub Repos, Commits and GitHub  

The set of all files versioned as a unit by the GIT source code control system is 
called a repository, or *repo* for short.   Logically a GIT repository contains a 
collection of snapshots called *commits* which represents a set of files at one
point in time.   The data in any such snapshot is hashed (including all its meta
data like when the commit was made), and this hash is the ID
for the commit.   In theory the ID is very long (64+ bytes), but in practice all
GIT commands will take a unambiguous prefix.  Typically 8 Hex digits is more than
sufficient so you will see instructions talking about commit 534aab38 or commit 3abcd333. 

One of the very nice properties of naming commits using a hash of the data is
that *combining two repos into a single one is trivial*.  If we assume that the
hashing GIT does is 'perfect' (and it really is close to perfect) if two commits
have the same ID (hash) then they have to have been the same data, which means
they really are the same commit.   Thus combining two repos is simply a matter of making
a union of all the commits in the two component repos.    

This ability to easily combine two repositories make GIT well suited for 'distributed'
source code control, where there is no 'master' repository that all 'clients' work 
from.   Instead each repository is can be thought of as the 'master', and a repository
like GitHub is only special in the sense that it is a well known publishing point.   

Indeed when working with GitHub you NEVER have just one repository.   At the minimum
there are two repositories you need to know about.   First is the repository hosted
on Github, and the other is a local repository on the machine where you make your 
changes.   This local repository is a COMPLETE CLONE of the GitHub one (which includes
all commits and thus all history).   Thus after cloning, you create many local 
changes (commits) without every communicating with GitHub.   Only when you want
to publish (calling pushing) do you need connectivity to GitHub.    

## Simple Git setup

If one of the following two conditions hold

1. You have Read/Write permission to the GitHub repository of interest.
2. You only have read access, but you never want to 'push' changes back to the repository.  

You can use a simple Git setup with only two repositories (the one on GitHub and the local
repository on your development machine).   In this setup you can pull new commits
from the GitHub repository to your local one to keep your local copy up to date.  
You can even make local changes for your own personal experimentation in your local repository.
However you can only 'push' those changes back to GitHub if you have write permission to
the repo.  This is fine for personal projects, but not good enough for open source projects.  

## Open Source Projects and Pull Requests

In an open source project, we want the ability for ANYONE to PROPOSE a change 
to the main repository, but we need an APPROVAL process so that only those with
special permissions (the maintainers) can actually update the main repository.  
GitHub accomplishes this with a process know as the *pull request*.   A pull request
is GitHub procedure where any user can indicate that he would like a particular change
(commit) integrated to the GitHub repository.   People who do have write permission
to the repository can look at the change and determine if it meets
the standards of the repository and if so merge (pull) the change into it.   In this way
arbitrary users can contribute fixes to a repository in a controlled way.   
   
There is a logistic problem with pull requests.  They need a commit (set of file changes)
to be submitted (and thus public), but this commit can't be created the main repository 
(because it was created by a user without write permissions to the main repository).   
It also can't be in the local repository because that is on a arbitrary user's machine and
not available to GitHub in general.    Thus we need a  repository that is in a public place 
(that is on GitHub), but is writeable by a particular user.   GitHub solves this problem 
by making a 3rd repository which is typically called *fork*.   

Thus the typical flow for an open source project is 

 1. Create a *Fork* (copy of the repo) of the open source repository (e.g. https://github.com/Microsoft/perfview) 
 that is writable by you.  There is a button on the project's  GitHub page (upper right corner called 'fork') that does
 this.   Every GitHub user has a 'area' assigned to their GitHub user name that can host these writable forks.  There may
 be other locations (shared by a group) that you may put the fork, so when clicking the 'Fork' button may prompt 
 you with choices of where to put it.   Typically you want to use the area associated with just your user.  For example my
 GitHub user name is vancem, so the fork created when I click on fork button of https://github.com/Microsoft/perfview creates 
 the fork called https://github.com/vancem/perfview (notice it in the 'vancem' area of GitHub which is writable by me).   
 If the fork already exists, it simply takes me there (so you can find your existing forks easily).

 2. Clone your GitHub fork locally on your machine.   This works just like if you had cloned the
 https://github.com/Microsoft/perfview directly, but with the significant difference that you can write 
 to this repository, so you can submit changes.

 3. Make changes (commits), and push them to your GitHub fork.   Note that you can do this many times
 but generally you want to keep your changes small because it makes it easier to review your pull request.  

 4. Once you have a commit you are happy with in your own fork, there is a button on YOUR FORKs github page that 
 allow you to submit that commit as a pull request.   When you do this you write a description / rationale for The
 change where you are persuading the maintainers to accept your request.   THere is an area for discussion, and typically
 the maintainers ask for changes.   You update your commit as needed until finally the maintainers accept
 the pull request and it becomes part of the main repository.  

## To Fork or not to Fork?

 So there are two different ways you can set up your clone of the repository

 1. You can simply clone the repository locally work from it.
 2. You can create a fork, and then clone fork locally and work with that. 

The basic answer is that (2) can do pull requests (and generally forces all updates to be pull requests), 
however it is also more cumbersome (syncing to the latest code is harder and updates have the overhead of the
pull request procedure).   So the simple answer is

1. Use (1) when you have read-write access and want a low overhead checkin process (private projects typically fall into this bucket).
2. Use (1) if you have read-only access and don't need to submit fixes/changes (thus you don't need pull requests)

A typical scenarios is that start with the direct (unforked) option up until the point where you want to start modifying the 
code base at which point you switch to forked option. 

##Setting Up *Without* a Fork with Visual Studio 2015 (Option 1)

Here we describe how to use Visual Studio 2015 to set a local build of the GitHub project https://github.com/Microsoft/perfview.   
Because this project is open source, anyone can read it so security 

	1. Open Visual Studio and select the View -> Team Explorer menu item
	2. At the top of the display is a bold word (probably Home) which indicates which view is being displayed 
	   if you left click on this you will get a menu of possible displays.   Select Projects -> Managed Connections
	3. At the bottom of this display you should see a section called 'Local Git Repositories'.  and under this a
	   set of hyperlinks.   Click on the 'Clone' hyperlink.
	4. It will bring up a dialog box where you enter two pieces of information 
	   a. The name of the source to clone.  Enter the GitHub URL (in this case https://github.com/Microsoft/perfview)
	   b. The name of the local directory to place the clone (The default is probably OK, but you can decide)
	5. Click the Clone button. 
	
At this point Visual Studio will create a local clone all the files in a project in the directory you specified.  It also
clones the whole history of the repository and places it in a hidden .git directory in the directory you specified.   It is
this cloned local repository where most GIT commands you do operate. 

Once the clone is complete, you will see the new local repository added to the list under 'Local Local Repositories'.   
You can see which one is 'active' because it is bold-faced.   This is the repository that all other VS GIT commands will
operate on.   Double click on the new entry (perfView in this example) to make it the active GIT repos.

This will bring you to the 'Home' display for a local GIT repository.   YOu will see buttons for 'changes', 'branches' 'sync', etc. 
At the bottom you will see a list of .SLN files.  There are all the Visual Studio solution files that where found.   This
makes it convenient to open VS to do a build.  In the case of PerfView, you want the PerfView.sln file.   After opening
the solution file you can start building (Build -> Build Solution or Ctrl-Shift-B).

##Setting Up *With* a Fork with Visual Studio 2015 (Option 2)

 As mentioned, the first option is great if you only want read-only access or you have read-write access to the GitHub repo.
 If you want to be able to submit pull requests, you really should create a fork (which you will have read-write access to)
 and use that for your updates.   To do this do the following

     1. Open a web browser to the GitHub project page (in our case https://github.com/Microsoft/perfview)
	 2. Make sure you have a GitHub account and that you are logged in (If you are logged into GitHub your user name 
	    in the very upper right corner of display will be an icon for your user identity).  See https://github.com
		for information on getting an account.   You should not need git tools if you have Visual Studio, but it also
		does not hurt to have them.  
	 3. Click on the 'Fork' button in the upper right corner.   If it asks you were to put he fork, you will wish
	    to put it in your account area (same as your user name).   Note that if the fork already exists it simply
		navigates to the existing fork, so this is also a way of finding a fork if needed. 
	 4. Once that has completed (it does not take long), it will take you to a page that looks VERY similar to 
	    the original but is in fact a clone.   It should have a URL like https://github.com/vancem/perfview that has  
		your user name in it.  
	 5. At this point you can follow the instructions above for setting without a fork.  The only difference is 
	    that the GitHub URL will be the URL of the fork and not the main repository.   You may also wish to give
		it a local name that makes it clear that it is a fork (for example I call my fork's directory perfview-vancem) 
		so it is very clear from its path that it is a clone of my personal fork and not a clone of the main repository.  

### Creating a 'upstream' Repository alias.  

At this point you should have a local clone of your personal read-write fork of the main repository.    However this
is a FULLY INDPENDENT clone which will NEVER SEE ANY UPDATES from the main repository unless you explicitly integrate 
(merge) them.   To make easy to do this updating, it is good to create an alias for the original master repository.  Here
we show you how to do that in Visual Studio 2015.  

  1. Click on the View -> Team Explorer menu item 
  2. Click on the top bolded header (may say 'Home' or 'Changes', ...) and right click on 'Settings'
  3. Click on the 'Repository Setting' hyperlink
  4. One of the sub-item in Repository setting page is 'Remotes' and under that is a 'Add' hyperlink.  Click on that.
  5. This will bring up a dialog box where you are prompted for a Name and a 'Fetch' and 'Push' location.  it
     There is also a checkbox (which should be checked) that says the Fetch and Push are the same. 
  6. Fill in the name as 'upstream' and fill the 'Fetch' textbox with the URL OF THE MAIN RESPOSITORY (in our    
     example this is https://github.com/Microsoft/perfview.  It should automatically fill in the 'Push' dialog.  
  7. Click the 'save' button.   This new name should now show up in the 'remotes' list.  

What this does is name the original man repository 'upstream'.    This makes it easier to refer to it in later commands
(like updating your fork with updates from the main repository).  You will see that your GitHub repo already has
a short name called 'origin' (this is very standard name to use for your 'cloud' repo that your local repo works with).
'upstream' is a traditional GIT name for the repository that a fork comes from.  

### Pulling updates from the main (upstream) repository into your Fork

 If you go to your fork's GitHub web page (e.g. one that has your user name in it like https://github.com/vancem/perfview)
 you will see that there is a line just before the description of files in the repo like
 
   * This branch is 4 commits behind Microsoft:master. 

This indicates how 'in sync' the fork is with the main repo (upstream).   From time to time you will want to pull in
all these changes to bring your fork up to date.   We do this in three steps

  1. Fetching all changes from the main repository (which we called upstream).  This step make all the changes (commits)
     in the fetched repository AVAILABLE for merging, but does not actually do any merging.  
  2. Merge the changes from the upstream/master branch into the LOCAL repository's master branch.
  3. Push the changes from the LOCAL repository's master branch into the fork master banch.  

####Step 1: Fetching All Commits for the Upstream Repository.  

  1. Click on the View -> Team Explorer menu item 
  2. Click on the bolded header (may say 'Home' or 'Changes', ...) and right click on 'Sync' option.  This will bring
     up the synchonization page, and one of the options will be 'Fetch'  click on that.   This brings up a dialog filled
	 out with 'origin' which says it will pull all changes from the 'origin' repository (which is your GitHub fork).
	 However we don't want that since we are interested in the changes from the original master (which we named upstream)
	 You should be able type a down arrow and 'upstream' should be one of the choices (since we added 'upstream' as a
	 known remote).    Select 'upstream' and click the 'Fetch' button.  That will fetch all the changes from 'upstream', 
	 and put them in the LOCAL Github repository.

####Step 2: Merging the LOCAL repository's master branch to include changes from upstream/master

  1. Click on the View -> Team Explorer menu item 
  2. Click on the top bolded header (may say 'Home' or 'Changes', ...) and right click on 'Branches' item which show you
     all branches associated with the LOCAL repository (that is the repository clone on your local disk).   There will
	 be at least one branch called 'master' as well as branches under 'remotes/origin' (these are branches in the
	 private fork on GitHub) and 'remotes/upstream' (these are branches in the original main repository).   
  3. Make sure that the 'active branch' (the one in bold) is 'master'.   If not double click on master to make it
     the active branch.  If it fails (and it could easily fail because you have modified files), you will have to
	 commit or undo those changes before proceeding.   Ultimately you want 'master' to be the active branch.  
  4. Click on the 'remotes/upstream/master' branch right click on it and select 'merge from'  This says we want
     to integrate (merge) all changes from the upstream master branch into the currently active branch (which is master).
	 A dialog will come up with text boxes filled out.  Simply click the 'Merge' button. 

In most cases this merge operation goes without a hitch and Visual Studio will auto-commit the merge.   However if there
is a merge conflict with changes that you have made to 'master' (you really should never do that, put changes in some other
branch (see below)), then you will need to resolve them and commit the merge explicitly.  

####Step 3: Pushing the LOCAL repository to your GitHub Fork

  1. Click on the View -> Team Explorer menu item 
  2. Click on the bolded header (may say 'Home' or 'Changes', ...) and right click on 'Sync' option.  This will bring
     up the synchonization page, and one of the options will be 'Sync'  click on that.   This will push all your 
	 changes to 'origin' (which is your GitHub fork) as well as pull down any changes from origin (there should be 
	 none as you are the only one modifying this fork).  

At this point you should be able to go to your GitHub web page for the fork and see a line like this

	This branch is even with Microsoft:master. 

Which says that you have integrated all the changes from the original main repository into your private fork.   

