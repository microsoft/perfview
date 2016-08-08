function StackDelegate(domain, filename, stackType, summaryStackData) {
    var self = this;
    self.filename = filename;
    self.stackType = stackType;
    self.summaryStackData = summaryStackData;
    self.domain = domain;
    self.focusNode = null;
    self.filters = "";
    self.numNodes = 1000;


    self.log = function log(status) {
        $("#statusBar span").text(status);
    };


    self.setFocusNode = function setFocusNode(node) {
        var child = node.row[0].children[0];

        $(child).find('span').remove();
        $(node).attr("id", $(child).text().trim());

        stackDelegate.focusNode = node;
    }


    self.getFocusNode = function getFocusNode() {
        return self.focusNode;
    }


    self.getSummaryData = function getSummaryData(callback) {
        var url = self.domain + "/api/data/stackviewer/summary?filename=" + self.filename
                                                          + "&stacktype=" + self.stackType
                                                          + "&numNodes=" + self.numNodes
                                                          + "&" + self.filters
                                                          + "&find=" + $("#by-name .find").val();
        self.log("Fetching Summary Data for " + self.filename);
        $.get(url, function (response, status) {
            json = JSON.parse(response);
            self.summaryStackData = json;

            self.log("Completed: Get Summary Data for " + self.filename);

            callback(json);
        });
    }


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


    self.getCallersData = function getCallersData(callback, options={}) {
        if (self.focusNode == null && !("overrideFocusNode" in options)) {
            throw "Focus node not selected";
        }

        var nodeString = "overrideFocusNode" in options ? options["overrideFocusNode"] : self.focusNode.id;
        var nameAndPath = nodeString.split(/\/(.+)?/);  // Split on FIRST occurrence of '/'
        var name = nameAndPath[0];
        var path = nameAndPath.length > 1 ? nameAndPath[1] : "";

        var url = self.domain + "/api/data/stackviewer/callertree?filename=" + self.filename
                                                                  + "&name=" + name
                                                             + "&stacktype=" + self.stackType
                                                                  + "&path=" + path
                                                                       + "&" + self.filters;
        if ("includeSearch" in options) { url = url + "&find=" + $("#callers .find").val(); }

        path = path != "" && path != undefined ? "/" + path : path;
        self.log("Fetching Callers Data for " + name + path);

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            self.log("Completed: Get Callers for " + name + path);
            
            callback(json, status);
        });
    }


    self.getCalleesData = function getCalleesData(callback, options={}) {
        if (self.focusNode == null && !("overrideFocusNode" in options)) {
            throw "Focus node not selected";
        }

        var nodeString = "overrideFocusNode" in options ? options["overrideFocusNode"] : self.focusNode.id;
        var nameAndPath = nodeString.split(/\/(.+)?/);  // Split on FIRST occurrence of '/'
        var name = nameAndPath[0];
        var path = nameAndPath.length > 1 ? nameAndPath[1] : "";

        var url = self.domain + "/api/data/stackviewer/calleetree?filename=" + self.filename
                                                                  + "&name=" + name
                                                             + "&stacktype=" + self.stackType
                                                                  + "&path=" + path
                                                                       + "&" + self.filters;
        if ("includeSearch" in options) { url = url + "&find=" + $("#callees .find").val(); }

        path = path != "" && path != undefined ? "/" + path : path;
        self.log("Fetching Callees Data for " + name + path);

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            self.log("Completed: Get Callees for " + name + path);

            callback(json, status);
        });
    }
}


var stackDelegate = new StackDelegate(window.opener.domain,
                                    window.opener.filename,
                                    window.opener.stackType,
                                    window.opener.summaryStackData);
