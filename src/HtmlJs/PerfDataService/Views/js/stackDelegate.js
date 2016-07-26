function StackDelegate(domain, filename, stackType, defaultNumNodes, summaryStackData) {
    var self = this;
    self.filename = filename;
    self.stackType = stackType;
    self.summaryStackData = summaryStackData;
    self.domain = domain;
    self.defaultNumNodes = defaultNumNodes;
    self.focusNode = null;
    self.filters = ""


    self.log = function log(status) {
        $("#statusBar span").text(status);
    };


    // TODO: Probably remove this
    //$('#tabs').on('change.zf.tabs', function (event, tab) {
    //    //console.log(tab);
    //});


    self.getSummaryData = function getSummaryData(callback) {
        var url = self.domain + "/api/data/stackviewer/summary?filename=" + self.filename
                                                          + "&stacktype=" + self.stackType
                                                          + "&numNodes=" + self.defaultNumNodes
                                                          + "&" + self.filters;

        self.log("Fetching Summary Data for " + self.filename);

        $.get(url, function (response, status) {
            json = JSON.parse(response);
            self.summaryStackData = json;

            self.log("Completed: Get Summary Data for " + self.filename);

            callback(json);
        });
    }


    self.getCallersData = function getCallersData(nodeName, path, callback) {
        var url = self.domain + "/api/data/stackviewer/callertree?filename=" + self.filename
                                                                  + "&name=" + nodeName
                                                             + "&stacktype=" + self.stackType
                                                              + "&numNodes=" + self.defaultNumNodes
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
                                                              + "&numNodes=" + self.defaultNumNodes
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
                                    window.opener.defaultNumNodes,
                                    window.opener.summaryStackData);
