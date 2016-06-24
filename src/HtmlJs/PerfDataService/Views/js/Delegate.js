function Delegate() {
    var self = this;
    var domain = "http://localhost:5000";

  this.log = function log(status) {
    $("#statusBar span").text(status);
  }

  this.httpGet = function httpGet(url, callback) {
    url = domain + url;
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = function() {
      if (xmlHttp.readyState == 4) {
        if (xmlHttp.status == 200) {
            self.log("GET " + url + " Complete");
            callback(JSON.parse(xmlHttp.responseText));
        } else {
          self.log("GET " + url + " " + xmlHttp.status);
        }
      }
    }
    xmlHttp.open("GET", url, true);
    xmlHttp.send(null);
  }
}

delegate = new Delegate();
