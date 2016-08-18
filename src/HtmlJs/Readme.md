# PerfDataService

PerfDataService is an ASP.NET Core powered web app that provides Linux users similar features to the PerfView application.
This is achieved by building a .NET Core compatible REST API that utilizes underlying components of the original PerfView application, in tandem with the TraceEvent library, and providing a web view that uses that API. This means you can:

  - Open .etl files and inspect stack data
  - Perform searches on stack data
  - Use many of the same powerful filter features that original PerfView application provides
  - All of this can be used on either Linux or Windows (*latest version of Chrome is required*)

### Tech

PerfDataService utilizes the following major resources, as well as many others not listed here:

* [HTML5] - Only the latest!
* [jQuery] - Javascript enhanced for web apps
* [Foundation] - great UI boilerplate for modern web apps
* [ASP.NET] - The backbone of the service

And of course the native **PerfView** application itself as built by [Vance Morrison].

### Installation

PerfDataService requires [npm](https://docs.npmjs.com/getting-started/installing-node) version **3.9.5** or greater.
If you already have npm, you can easily update it:
```
$ npm install npm -g
```
You can then confirm the version like so:
```sh
$ npm -v
$ 3.9.5
```
Now, you can download or clone from this repo:
```sh
$ git clone https://github.com/Microsoft/perfview.git
```

Install the client side (web view) dependencies:
```sh
$ cd perfview/src/HtmlJs/PerfDataService/Views
$ npm install
```

Now you're ready to start the project. Simply navigate to perfview/src/HtmlJs and you should see the following files and directories:
```
documentation
PerfDataService (folder)
PerfDataService (Microsoft Visual Studio Project File)
Readme
```
Simply open the *PerfDataService* project using Visual Studio version **14.0.25422.01 Update 3** or greater, and you can start profiling!

### Development

The PerfDataService doesn't have a lot of moving parts, but some of those parts have a decent amount of state that is important to know about. Here's a high level overview of the structure of the application...

##### Client Side
Starting on the client side, which can be found (in its entirety) in the `/Views` folder, are 3 main components:
* `js` folder
* `libraries` folder
* `static` folder

The `js` folder contains any non-libray javascript files (e.g. files that are included and used by the web pages that provide core functionality to the app). These consist of `homeDelegate.js` and `stackDelegate.js`. Both files contain a single "Delegate" class that has methods and properties used to handle all communication with the backend of the service. The properties act as the "state" (often manipulated by the user via the view) that changes the outcome of the methods. The outcome of the methods are mainly the construction of urls that are used in AJAX requests made to the service side. In particular, the main web page, `index.html`, uses a global `HomeDelegate` instance to make the AJAX calls to REST API endpoints that handle traversing the service's file system. Similarly, the stack view, `stackviewer.html`, uses a global `StackDelegate` instance to form all of the AJAX calls to the REST API that retrieves and searches all of the stack data.

The `libraries` folder simply contains NON-node_module libraries that are used by the client.

The `static` folder contains all of the HTML templates and css. Most of the things happening in this application derive from interactions with the `stackviewer.html` which is in this folder.

Note that there's also a `package.json` in this folder. This is the client-side dependencies list that npm uses to know what dependencies need to be installed. Do not confuse this with the `project.json` directly under the PerfDataService directory -- the project.json used by the Visual Studio Project Package Manager, NuGet.

##### Service Side
The service side has a fair number of moving parts, but there are a few main takeaways.

When a user is on the main page, traversing the filesystem, they are interacting strictly with the `DataController.cs` file under the Controllers directory. In particular, this utilizes a single REST endpoint:
* /api/[controller]/open

This endpoint simply navigates to the specified path on the filesystem and returns all of its child directories and relevant files to the client side. When the user clicks on a .etl or .etl.zip file, this endpoint will take the path to the .etl file and open it, returning all of its stacks to the client view. This opening event is done using PerfViewExtensibility, found in the `Models/Extensibility.cs`, a component of the native PerfView application. The user can then select a stack, at which point the `HomeDelegate` instance will open up the `stackviewer.html` page and provide it with the knowledge of which stack was selected, where the etl file is located, and other relevant meta data needed to open the stack view.

When a user is on the stack view page, traversing through nodes, searching for them, etc., they are interacting with `DataController.cs` as well as `StackViewerController.cs`. `DataController.cs` has the following endpoints for receiving stack/node requests:
* /api/[controller]/stackviewer/summary (GetStackSummary)
* /api/[controller]/stackviewer/node (GetNode)
* /api/[controller]/stackviewer/callertree (GetCallers of a specified node)
* /api/[controller]/stackviewer/calleetree (GetCallees of a specified node)

These endpoints act as the last layer that may have any attachment to the UI (e.g. cares about formatting the data in a way the UI can understand). Although at this point the endpoints remain fairly UI agnostic, any bias that must be imparted on the service side should go here. These endpoints communicate directly with symmetrical endpoints in the `StackViewerController.cs` controller. This is the controller that interacts with the model to actually retrieve and manipulate the stack data.

NOTE: An incredibly huge thank you is deserved by [Mukul Sabharwal](https://github.com/mjsabby) at Microsoft for providing a very large chunk of the model and REST API for this service.

### Todos

 - Divide StackDelegate class into separate Delegate classes for each tab (e.g. ByNameDelegate, CallTreeDelegate, CallersDelegate, CalleesDelegate) -- This would remove a fair amount of state that clutters the StackDelegate and stackviewer.html
 - Write Tests
 - Fix Remaining Urgent Bugs


[//]: # (These are reference links used in the body of this note and get stripped out when the markdown processor does its job. There is no need to format nicely because it shouldn't be seen.)

   [jQuery]: <http://jquery.com>
   [Foundation]: <http://foundation.zurb.com/>
   [ASP.NET]: <http://www.asp.net/>
   [Vance Morrison]: <https://github.com/vancem>
   [HTML5]: <https://developer.mozilla.org/en-US/docs/Web/Guide/HTML/HTML5>
