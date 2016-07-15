function StackDelegate(domain, filename, stackType, defaultNumNodes, summaryStackData) {
    var self = this;
    self.filename = filename;
    self.stackType = stackType;
    self.summaryStackData = summaryStackData;
    self.domain = domain;
    self.defaultNumNodes = defaultNumNodes;
    self.currentFilters = "";
    self.focusNode = "";

    self.log = function log(status) {
        $("#statusBar span").text(status);
    };

    $('#tabs').on('change.zf.tabs', function (event, tab) {
        //console.log(tab);
    });

    self.getCallerTree = function getCallerTree(name, path, filters, callback) {
        var url = self.domain + "/api/data/stackviewer/callertree?filename=" + self.filename
                                                                            + "&name=" + name
                                                                            + "&stacktype=" + self.stackType
                                                                            + "&numNodes=" + self.defaultNumNodes
                                                                            + "&path=" + path;
        if (self.currentFilters != "" && self.currentFilters != undefined) {
            url += "&" + self.currentFilters;
        }

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            // Log the completed work out
            //self.log("Completed: Get Callers for " + name + " at path: " + path);
            path = path != "" && path != undefined ? "/" + path : path;
            console.log("Completed: Get Callers for " + name + path);
            
            callback(json);
        });
    }

}


var stackDelegate = new StackDelegate(window.opener.domain,
                                    window.opener.filename,
                                    window.opener.stackType,
                                    window.opener.defaultNumNodes,
                                    window.opener.summaryStackData);
