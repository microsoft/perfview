function StackDelegate(domain, filename, stackType, summaryStackData) {
    var self = this;
    
    // These properties define the state of the stackDelegate
    self.filename = filename;
    self.stackType = stackType;
    self.summaryStackData = summaryStackData;
    self.domain = domain;
    self.focusNode = null;
    self.filters = "";
    self.numNodes = 1000;
    self.selectedCell = null;


    // Bottom status bar logger
    self.log = function log(status) {
        $("#statusBar span").text(status);
    };


    // This is called when the user double clicks on a node in the by-name-list, callersTree, or CalleesTree
    self.setFocusNode = function setFocusNode(node) {
        // Get the td element from the name column of this row
        var child = node.row[0].children[0];

        // Remove the span indenter tag, if there is one
        $(child).find('span').remove();
        $(node).attr("id", $(child).text().trim());  // Get the name text

        stackDelegate.focusNode = node;
    }


    self.getFocusNode = function getFocusNode() {
        return self.focusNode;
    }


    // This is called when the user single clicks on any cell
    self.setSelectedCell = function setSelectedCell(cell) {
        if (cell == null || cell === undefined) { return; }

        if (!cell.hasOwnProperty("selected") || cell.selected == false) {  // If cell is unselected
            var oldCell = self.selectedCell;
            changeCellState(oldCell, false);
            changeCellState(cell, true); // Change to selected state
            self.selectedCell = cell;
        } else if (cell.hasOwnProperty("selected") && cell.selected == true) {
            if (self.selectedCell != null && $(cell)[0] == $(self.selectedCell)[0]) { return; }
            changeCellState(cell, false);
            self.clearSelectedCell();
        }
    }

    // Helper function to setSelectedCell
    function changeCellState(cell, selected) {
        if (cell == null) { return; }

        if (selected) {
            $(cell).addClass("selected");  // This causes the styling to show up (look in stackviewer.css for more)
            cell.selected = true;
        } else {
            $(cell).removeClass("selected");  // This causes the styling to return to normal (look in stackviewer.css for more)
            cell.selected = false;
        }
    }

    self.getSelectedCell = function getSelectedCell() {
        return self.selectedCell;
    }

    self.clearSelectedCell = function clearSelectedCell() {
        if (self.selectedCell == null) { return; }
        changeCellState(self.selectedCell, false);
    }


    // Returns the summary stack based on the properties set in this stackDelegate
    self.getSummaryData = function getSummaryData(callback, options={}) {
        var url = self.domain + "/api/data/stackviewer/summary?filename=" + self.filename
                                                          + "&stacktype=" + self.stackType
                                                          + "&numNodes=" + self.numNodes
                                                          + "&" + self.filters;
        // If the caller indicated that the service should subsequently perform a search on this stack
        if ("includeSearch" in options) { url = url + "&find=" + $("#by-name .find").val(); }

        self.log("Fetching Summary Data for " + self.filename);
        $.get(url, function (response, status) {
            json = JSON.parse(response);
            self.summaryStackData = json;

            self.log("Completed: Get Summary Data for " + self.filename);

            callback(json);
        });
    }


    // Returns a single node
    self.getNode = function getNode(name, callback) {
        var url = self.domain + "/api/data/stackviewer/node?filename=" + self.filename
                                                          + "&stacktype=" + self.stackType
                                                          + "&name=" + name;

        self.log("Fetching Node Data for " + name);
        $.get(url, function (response, status) {
            json = JSON.parse(response);

            self.log("Completed: Get Node Data for " + name);

            callback(json);
        });
    }


    // Returns the callers data stack based on the state of the properties in this stackDelegate
    self.getCallersData = function getCallersData(callback, options={}) {
        if (self.focusNode == null && !("overrideFocusNode" in options)) {
            throw "Focus node not selected";
        }

        // overrideFocusNode is used when loading the ROOT for the CallTree table and by onTreeNodeExpand(),
        // both in stackviewer.html
        var nodeString = "overrideFocusNode" in options ? options["overrideFocusNode"] : self.focusNode.id;
        var nameAndPath = nodeString.split(/\/(.+)?/);  // Split on FIRST occurrence of '/'
        var name = nameAndPath[0];
        var path = nameAndPath.length > 1 ? nameAndPath[1] : "";

        var url = self.domain + "/api/data/stackviewer/callertree?filename=" + self.filename
                                                                  + "&name=" + name
                                                             + "&stacktype=" + self.stackType
                                                                  + "&path=" + path
                                                                       + "&" + self.filters;
        // If the caller indicated that the service should subsequently perform a search on this stack
        if ("includeSearch" in options) { url = url + "&find=" + $("#callers .find").val(); }

        path = path != "" && path != undefined ? "/" + path : path;
        self.log("Fetching Callers Data for " + name + path);

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            self.log("Completed: Get Callers for " + name + path);
            
            callback(json, status);
        });
    }


    // Returns the callers data stack based on the state of the properties in this stackDelegate
    self.getCalleesData = function getCalleesData(callback, options={}) {
        if (self.focusNode == null && !("overrideFocusNode" in options)) {
            throw "Focus node not selected";
        }

        // overrideFocusNode is used when loading the ROOT for the CallTree table and by onTreeNodeExpand(),
        // both in stackviewer.html
        var nodeString = "overrideFocusNode" in options ? options["overrideFocusNode"] : self.focusNode.id;
        var nameAndPath = nodeString.split(/\/(.+)?/);  // Split on FIRST occurrence of '/'
        var name = nameAndPath[0];
        var path = nameAndPath.length > 1 ? nameAndPath[1] : "";

        var url = self.domain + "/api/data/stackviewer/calleetree?filename=" + self.filename
                                                                  + "&name=" + name
                                                             + "&stacktype=" + self.stackType
                                                                  + "&path=" + path
                                                                       + "&" + self.filters;
        // If the caller indicated that the service should subsequently perform a search on this stack
        if ("includeSearch" in options && "call-treeTree" in options) {
            url = url + "&find=" + $("#call-tree .find").val();
        } else if ("includeSearch" in options) {
            url = url + "&find=" + $("#callees .find").val();
        }

        path = path != "" && path != undefined ? "/" + path : path;
        self.log("Fetching Callees Data for " + name + path);

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            self.log("Completed: Get Callees for " + name + path);

            callback(json, status);
        });
    }
}


// This means the StackDelegate constructor gets called when stackviewer.html loads this javascript file
// The parameters passed to the constructor are grabbed off of the window/tab (index.html) that opened this stack page up
var stackDelegate = new StackDelegate(window.opener.domain,
                                    window.opener.filename,
                                    window.opener.stackType,
                                    window.opener.summaryStackData);
