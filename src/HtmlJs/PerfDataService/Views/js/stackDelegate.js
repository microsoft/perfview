function StackDelegate(domain, filename, stackType, summaryStackData) {
    var self = this;
    self.filename = filename;
    self.stackType = stackType;
    self.summaryStackData = summaryStackData;
    self.domain = domain;
    self.focusNode = null;
    self.filters = "";


    self.log = function log(status) {
        $("#statusBar span").text(status);
    };


    self.getSummaryData = function getSummaryData(callback) {
        var url = self.domain + "/api/data/stackviewer/summary?filename=" + self.filename
                                                          + "&stacktype=" + self.stackType
                                                          + "&numNodes=-1&" + self.filters;
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


    self.getCallersData = function getCallersData(nodeName, path, callback) {
        var url = self.domain + "/api/data/stackviewer/callertree?filename=" + self.filename
                                                                  + "&name=" + nodeName
                                                             + "&stacktype=" + self.stackType
                                                                  + "&path=" + path
                                                                       + "&" + self.filters;

        path = path != "" && path != undefined ? "/" + path : path;
        self.log("Fetching Callers Data for " + nodeName + path);

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            self.log("Completed: Get Callers for " + nodeName + path);
            
            callback(json, status);
        });
    }


    self.getCalleesData = function getCalleesData(nodeName, path, callback) {
        var url = self.domain + "/api/data/stackviewer/calleetree?filename=" + self.filename
                                                                  + "&name=" + nodeName
                                                             + "&stacktype=" + self.stackType
                                                                  + "&path=" + path
                                                                       + "&" + self.filters;

        path = path != "" && path != undefined ? "/" + path : path;
        self.log("Fetching Callees Data for " + nodeName + path);

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            self.log("Completed: Get Callees for " + nodeName + path);

            callback(json, status);
        });
    }
}


var stackDelegate = new StackDelegate(window.opener.domain,
                                    window.opener.filename,
                                    window.opener.stackType,
                                    window.opener.summaryStackData);
