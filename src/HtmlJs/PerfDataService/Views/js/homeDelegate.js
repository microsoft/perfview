function HomeDelegate() {
    self = this;
    self.domain = "http://localhost:5000";
    self.defaultDirectoryTreePath = "";  // TODO: Get rid of this?
    self.defaultNumNodes = 10;
    self.treeDivID = "#treeContainer";

    self.log = function log(status) {
        $("#statusBar span").text(status);
    };

    self.changeDirectoryTreePath = function changeDirectoryTreePath(path) {
        url = self.domain + "/api/data/open?path=" + path;
        $.get(url, function (response, status) {
            json = JSON.parse(response);
            self.log("GET " + url + " Complete");
            createJsTreeFromJSON(json);
        });
    };


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

        self.log("Completed: Directory Tree Path Update");
    };


    function addJsTreeEventListeners() {
        // Directory/File Click Event
        $(self.treeDivID).on('activate_node.jstree', function (event, node) {
            nodeObject = node.node.original;  // JSTree has a node within a node.. Weird.

            if (nodeObject.type == "dir" || nodeObject.type == "file") {
                self.changeDirectoryTreePath(nodeObject.path);
            } else if (nodeObject.type == "stacks") {
                self.openStackSummary(nodeObject.path, nodeObject.stackType, self.defaultNumNodes);
            }
        });
    }

    self.openStackSummary = function openStackSummary(filename, stackType, numNodes = self.defaultNumNodes) {
        var url = self.domain + "/api/data/stackviewer/summary?filename=" + filename + "&stacktype=" + stackType + "&numNodes=" + numNodes;
        
        console.log( url );

        $.get(url, function (response, status) {
            json = JSON.parse(response);
            console.log( json );
            // Attach data to current (parent) window so that the new window can access it on load (via window.opener.summaryStackData)
            window.domain = self.domain;
            window.filename = filename;
            window.stackType = stackType;
            window.defaultNumNodes = self.defaultNumNodes;
            window.summaryStackData = json;

            console.log(self.domain);

            // Create and open the new window
            var stackViewerWindow = window.open(self.domain + "/Views/static/stackviewer.html");

            // Log the completed work out
            self.log("Completed: Open " + stackType + " Stack Summary at filepath " + filename);
        });
    }

}

homeDelegate = new HomeDelegate();
