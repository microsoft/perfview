function Delegate() {
    var self = this;
    self.domain = "http://localhost:5000";
    self.defaultDirectoryTreePath = "C:/Users/t-kahoop/Development/perfview/src/PerfView/bin/Debug";
    self.defaultNumNodes = 10;
    self.treeDivID = "#treeContainer";

    self.log = function log(status) {
        $("#statusBar span").text(status);
    };

    // PRIVATE
    function httpGet(url, callback) {
        url = self.domain + url;
        var xmlHttp = new XMLHttpRequest();
        xmlHttp.onreadystatechange = function () {
            if (xmlHttp.readyState == 4) {
                if (xmlHttp.status == 200) {
                    self.log("GET " + url + " Complete");
                    callback(JSON.parse(xmlHttp.responseText));
                } else {
                    self.log("GET " + url + " " + xmlHttp.status);
                }
            }
        };
        xmlHttp.open("GET", url, true);
        xmlHttp.send(null);
    };

    self.changeDirectoryTreePath = function changeDirectoryTreePath(path) {
        httpGet("/api/data/open?path=" + path, function (response) {
            console.log(response);
            createJsTreeFromJSON(response);
            delegate.log("Completed: Directory Tree Path Update");
        });
    };

    self.openStackSummary = function openStackSummary(filePath, stackType, numNodes) {
        httpGet("/api/data/stackviewer/summary?filename=" + filePath + "&stacktype=" + stackType + "&numNodes=" + numNodes, function (response) {
            console.log(response);
            delegate.log("Completed: Open " + stackType + " Stack Summary at filepath " + filePath);
        });
    }

    // PRIVATE
    function createJsTreeFromJSON(JSONTree) {
        // In case there is a tree already loaded in the div, destroy it
        $(self.treeDivID).jstree("destroy");

        // Load the JSON data
        $(self.treeDivID).jstree({
            'core': {
                'data': JSONTree.children
            }
        });

        // Add JsTree event listeners (handles user interactions with the tree)
        addJsTreeEventListeners();
    };

    // PRIVATE
    function addJsTreeEventListeners() {
        $(self.treeDivID).on('activate_node.jstree', function (event, node) {
            nodeObject = node.node.original;  // JSTree has a node within a node.. Weird.

            if (nodeObject.type == "dir" || nodeObject.type == "file") {
                self.changeDirectoryTreePath(nodeObject.path);
            } else if (nodeObject.type == "stacks") {
                self.openStackSummary(nodeObject.path, nodeObject.stackType, self.defaultNumNodes);
            }
        });

        // TODO: Use double click event to change directory
        //$(self.treeDivID).on('dblclick.jstree', function (event, node) {
        //    var node = $(event.target).closest("li");
        //    var node_id = node[0].id
        //    var newPath = $(self.treeDivID).find(node_id);

        //    console.log(newPath);
        //    self.changeDirectoryTreePath(node.node.original.path);
        //});
        
        // TODO: Get dropdown arrows working
        //$(self.treeDivID).on('load_node.jstree', function (event, data) {
        //    var treeNode = data.node;
        //    if (treeNode.type === NODE_TYPE_FOLDER) {
        //        domNode = $(self.treeDivID).jstree.get_node(treeNode.id, true);
        //        domNode.addClass('jstree-open').removeClass('jstree-leaf');
        //    }
        //});
    }

}

delegate = new Delegate();



/**********************************************************/
/**********************************************************/
/**************** USING THE DELEGATE BELOW ****************/
/**********************************************************/
/**********************************************************/


$(document).ready(function () {
    delegate.changeDirectoryTreePath(delegate.defaultDirectoryTreePath);
    // TODO: Pull these from a cache file of the 10 most recently options the user has chosen
    $("#recentDirectories").append("<option value=" + delegate.defaultDirectoryTreePath + "/>");
});
