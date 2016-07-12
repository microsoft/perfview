function StackDelegate() {
    var self = this;
    self.domain = "http://localhost:50001";
    self.currentNode = "";
    self.defaultNumNodes = 10;

    self.log = function log(status) {
        $("#statusBar span").text(status);
    };

    $('#tabs').on('toggled', function (event, tab) {
        console.log("tab change!")
        console.log(tab);
    });

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

stackDelegate = new StackDelegate();
