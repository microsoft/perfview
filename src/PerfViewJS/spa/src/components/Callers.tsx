import { EmptyTNode, TNode } from './TNode';

import { Link } from 'react-router-dom'
import { NavMenu } from './NavMenu';
import React from 'react';
import { StackViewerFilter } from './StackViewerFilter'
import { TreeNode } from './TreeNode'
import base64url from 'base64url'

export interface Props {
    match: any;
}

interface State {
    loading: boolean;
    node: TNode;
}

export class Callers extends React.Component<Props, State> {

    ignoreLastFetch: boolean;

    static displayName = Callers.name;

    fetchData() {

        this.setState({ node: new EmptyTNode(), loading: true }); // HACK: Why is this required?

        fetch('/api/treenode?' + Callers.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey) + '&name=' + this.props.match.params.callTreeNodeId + '&subtree=' + this.props.match.params.subtree, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                if (!this.ignoreLastFetch) {
                    if (data === null) {
                        window.location.href = '/ui/stackviewer/hotspots/' + this.props.match.params.routeKey;
                    } else {
                        data.hasChildren = true; // HACK: Because the api doesn't return this set true.
                        this.setState({ node: data, loading: false });
                    }
                }
            });
    }

    constructor(props: Props) {
        super(props);
        this.ignoreLastFetch = false;
        this.state = { loading: true, node: new EmptyTNode() };
    }

    componentWillUnmount() {
        this.ignoreLastFetch = true
    }

    componentDidMount() {
        this.fetchData();
    }

    componentDidUpdate(prevProps: Props) {
        let oldId = prevProps.match.params.callTreeNodeId
        let newId = this.props.match.params.callTreeNodeId
        if (newId !== oldId) {
            this.fetchData()
        }
    }

    static constructAPICacheKeyFromRouteKey(r: string) {
        var routeKey = JSON.parse(base64url.decode(r, "utf8"));
        return 'filename=' + routeKey.a + '&stackType=' + routeKey.b + '&pid=' + routeKey.c + '&start=' + base64url.encode(routeKey.d, "utf8") + '&end=' + base64url.encode(routeKey.e, "utf8") + '&groupPats=' + base64url.encode(routeKey.f, "utf8") + '&foldPats=' + base64url.encode(routeKey.g, "utf8") + '&incPats=' + base64url.encode(routeKey.h, "utf8") + '&excPats=' + base64url.encode(routeKey.i, "utf8") + '&foldPct=' + routeKey.j + '&drillIntoKey=' + routeKey.k;
    }

    static renderCallersTable(routeKey: string, callTreeNodeId: string, node: TNode) {
        return (
            <table className="table table-striped table-bordered" id="pd">
                <thead>
                    <tr>
                        <td className="center">Name</td>
                        <td className="right">Source</td>
                        <td className="center">Exclusive Metric %</td>
                        <td className="center">Exclusive Count</td>
                        <td className="center">Inclusive Metric %</td>
                        <td className="center">Inclusive Count</td>
                        <td className="center">Fold Count</td>
                        <td className="center">When</td>
                        <td className="center">First</td>
                        <td className="center">Last</td>
                    </tr>
                </thead>
                <tbody>
                    <TreeNode routeKey={routeKey} callTreeNodeId={callTreeNodeId} node={node} indent={0} autoExpand={node.hasChildren} />
                </tbody>
            </table>
        );
    }

    render() {
        let contents = this.state.loading ? <p><em>Loading...</em></p> : Callers.renderCallersTable(this.props.match.params.routeKey, this.props.match.params.callTreeNodeId, this.state.node);

        return (
            <div>
                <NavMenu dataFile={JSON.parse(base64url.decode(this.props.match.params.routeKey, "utf8")).a} />
                <div style={{ margin: 2 + 'px' }}>
                    <div style={{ margin: 10 + 'px' }}>
                        <h4>{base64url.decode(JSON.parse(base64url.decode(this.props.match.params.routeKey, "utf8")).l, "utf8")} >> <Link to={`/ui/stackviewer/hotspots/${this.props.match.params.routeKey}`}>Hotspots</Link> >> Callers</h4>
                        <StackViewerFilter routeKey={this.props.match.params.routeKey}></StackViewerFilter>
                    </div>
                    {contents}
                </div>
            </div>

        );
    }
}