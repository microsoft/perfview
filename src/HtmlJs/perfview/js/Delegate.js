function Delegate() {
  var self = this;

  this.log = function log(status) {
    $("#statusBar span").text(status);
  }

  this.httpGet = function httpGet(url, callback) {
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = function() {
      if (xmlHttp.readyState == 4) {
        if (xmlHttp.status == 200) {
          self.log("GET " + url + " Complete");
          callback(xmlHttp.responseText);
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
