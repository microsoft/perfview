function Delegate() {
    var self = this;
    self.domain = "http://localhost:5000";
    self.defaultDirectoryTreePath = "C:/Users/t-kahoop/Development/perfview/src/PerfView/bin/Debug";

  this.log = function log(status) {
    $("#statusBar span").text(status);
  }

  this.httpGet = function httpGet(url, callback) {
    url = self.domain + url;
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

  this.changeDirectoryTreePath = function changeDirectoryTreePath(path) {
      self.httpGet("/api/data/open?path=" + path, function (response) {
          console.log(response);
          var items = response.children;
          var htmlFromJSON = createTreeFromJSON(items);
          $("#directoryTree").html(htmlFromJSON);
          delegate.log("Completed: Directory Tree Path Update");
      });
  }
}

delegate = new Delegate();
