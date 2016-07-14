function StackDelegate(filename, stackType, summaryStackData) {
    var self = this;
    self.filename = filename;
    self.stackType = stackType;
    self.summaryStackData = summaryStackData;
    self.domain = "http://localhost:5000";
    self.currentNode = "";
    self.defaultNumNodes = 10;

    self.log = function log(status) {
        $("#statusBar span").text(status);
    };

    $('#tabs').on('change.zf.tabs', function (event, tab) {
        //console.log(tab);
    });

    self.getCallerTree = function getCallerTree(name, path, callback) {
        var url = self.domain + "/api/data/stackviewer/callertree?filename=" + self.filename
                                                                            + "&name=" + name
                                                                            + "&stacktype=" + self.stackType
                                                                            + "&numNodes=" + self.defaultNumNodes
                                                                            + "&path=" + path;
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


var filename = window.opener.filename;
var stackType = window.opener.stackType;
var summaryStackData = window.opener.summaryStackData;

var stackDelegate = new StackDelegate(filename, stackType, summaryStackData);
