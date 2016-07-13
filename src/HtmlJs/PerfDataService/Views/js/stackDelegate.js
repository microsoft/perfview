function StackDelegate(filename, stackType, stackData) {
    var self = this;
    self.filename = filename;
    self.stackType = stackType;
    self.stackData = stackData;
    self.domain = "http://localhost:5000";
    self.currentNode = "";
    self.defaultNumNodes = 10;

    self.log = function log(status) {
        $("#statusBar span").text(status);
    };

    // TODO: If By Name row was selected, change to callers tree
    $(document).dblclick("#tree tbody tr", function (row) {
        var name = row.target.childNodes[1].data;  // Get the name of the node

        if (name !== undefined) {
            self.getCallers(self.filename, name, self.stackType, self.defaultNumNodes);
        }
    });

    //$('#tabs').foundation('selectTab', $("#caller-callee"));

    $('#tabs').on('change.zf.tabs', function (event, tab) {
        console.log(tab);
    });

    self.getCallers = function getCallers(filename, name, stackType, numNodes) {
        var url = self.domain + "/api/data/stackviewer/callertree?filename=" + filename + "&name=" + name + "&stacktype=" + stackType + "&numNodes=" + numNodes;

        $.get(url, function (response, status) {
            json = JSON.parse(response);

            console.log(json);

            // Log the completed work out
            //self.log("Completed: Get Callers for " + name);
        });
    }

    //self.httpGet = function httpGet(url, callback) {
    //    url = self.domain + url;
    //    var xmlHttp = new XMLHttpRequest();
    //    xmlHttp.onreadystatechange = function () {
    //        if (xmlHttp.readyState == 4) {
    //            if (xmlHttp.status == 200) {
    //                self.log("GET " + url + " Complete");
    //                callback(JSON.parse(xmlHttp.responseText));
    //            } else {
    //                self.log("GET " + url + " " + xmlHttp.status);
    //            }
    //        }
    //    };
    //    xmlHttp.open("GET", url, true);
    //    xmlHttp.send(null);
    //};

}


var filename = window.opener.filename;
var stackType = window.opener.stackType;
var stackData = window.opener.stackData;

var stackDelegate = new StackDelegate(filename, stackType, stackData);
