import React, { PureComponent } from 'react';
import { Link } from 'react-router-dom'
import base64url from 'base64url';

export class TreeNode extends PureComponent {
    static displayName = TreeNode.name;

    constructor(props) {
        super(props);
        this.state = { children: [], isCollapsed: true };
        this.toggleTreeNode = this.toggleTreeNode.bind(this);
        this.handleDrillIntoClick = this.handleDrillIntoClick.bind(this);
    }

    handleDrillIntoClick(d) {

        var drillType = '/api/drillinto/exclusive?'
        if (d === 'i') {
            drillType = '/api/drillinto/inclusive?';
        }

        fetch(drillType + TreeNode.constructAPICacheKeyFromRouteKey(this.props.routeKey) + '&name=' + this.props.callTreeNodeId + '&path=' + this.props.node.path, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
        .then(res => res.json())
        .then(data => {
            var newRouteKey = JSON.parse(base64url.decode(this.props.routeKey, "utf8"));
            newRouteKey.k = data;
            window.location.href = '/ui/hotspots/' + base64url.encode(JSON.stringify(newRouteKey));
        });
    }

    toggleTreeNode() {
        if (this.state.isCollapsed) {
            this.expandTreeNode();
        }
        else {
            this.collapseTreeNode();
        }
    }

    collapseTreeNode() {
        this.setState({ children: [], isCollapsed: true });
    }

    expandTreeNode() {
        fetch('/api/callerchildren?' + TreeNode.constructAPICacheKeyFromRouteKey(this.props.routeKey) + '&name=' + this.props.callTreeNodeId + '&path=' + this.props.node.path, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => { this.setState({ children: data, isCollapsed: false }); });
    }

    static constructAPICacheKeyFromRouteKey(r) {
        var routeKey = JSON.parse(base64url.decode(r, "utf8"));
        return 'filename=' + routeKey.a + '&stackType=' + routeKey.b + '&pid=' + routeKey.c + '&start=' + base64url.encode(routeKey.d, "utf8") + '&end=' + base64url.encode(routeKey.e, "utf8") + '&groupPats=' + base64url.encode(routeKey.f, "utf8") + '&foldPats=' + base64url.encode(routeKey.g, "utf8") + '&incPats=' + base64url.encode(routeKey.h, "utf8") + '&excPats=' + base64url.encode(routeKey.i, "utf8") + '&foldPct=' + routeKey.j + '&drillIntoKey=' + routeKey.k;
    }

    static renderTreeNode(routeKey, node, indent, callTreeNodeId, children, isCollapsed, toggleTreeNode, obj) {
        return (
            <React.Fragment>
                <tr>
                    <td style={{paddingLeft: indent + 'px'}}>{node.hasChildren && <button className="btn btn-secondary btn-tiny" onClick={toggleTreeNode}>{isCollapsed ? "+" : "-"}</button>} <Link to={`/ui/callers/${routeKey}/${node.base64EncodedId}`}>{node.name}</Link></td>
                    <td className="center">{node.exclusiveMetricPercent}%</td>
                    <td className="center"><button onClick={() => obj.handleDrillIntoClick('e')}>{node.exclusiveCount}</button></td>
                    <td className="center">{node.inclusiveMetricPercent}%</td>
                    <td className="center"><button onClick={() => obj.handleDrillIntoClick('i')}>{node.inclusiveCount}</button></td>
                    <td className="center">{node.exclusiveFoldedMetric}</td>
                    <td className="center">{node.inclusiveMetricByTimeString}</td>
                    <td className="center">{node.firstTimeRelativeMSec}</td>
                    <td className="center">{node.lastTimeRelativeMSec}</td>
                </tr>
                {children.map(child => <TreeNode key={`${child.base64EncodedId}`} routeKey={routeKey} node={child} indent={indent + 10} callTreeNodeId={callTreeNodeId}></TreeNode>)}
            </React.Fragment>
        );
    }

    render() {
        return TreeNode.renderTreeNode(this.props.routeKey, this.props.node, this.props.indent, this.props.callTreeNodeId, this.state.children, this.state.isCollapsed, this.toggleTreeNode, this);
    }
}
