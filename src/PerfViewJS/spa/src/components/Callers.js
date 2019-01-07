import React, { Component } from 'react';
import { TreeNode } from './TreeNode'
import { StackViewerFilter } from './StackViewerFilter'
import { Link } from 'react-router-dom'
import base64url from 'base64url'
import './Callers.css';

export class Callers extends Component {
    static displayName = Callers.name;

    fetchData() {

        this.setState({ node: null, loading: true }); // HACK: Why is this required?

        fetch('/api/treenode?' + Callers.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey) + '&name=' + this.props.match.params.callTreeNodeId + '&subtree=' + this.props.match.params.subtree, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => { 
                if (!this.ignoreLastFetch) {
                    data.hasChildren = true; // HACK: Because the api doesn't return this set true.
                    this.setState({ node: data, loading: false });
                }
        });
    }

    constructor(props) {
        super(props);
        this.state = { loading: true };
    }

    componentWillUnmount () {
        this.ignoreLastFetch = true
      }

    componentDidMount () {
        this.fetchData()
      }
    
    componentDidUpdate (prevProps) {
        let oldId = prevProps.match.params.callTreeNodeId
        let newId = this.props.match.params.callTreeNodeId
        if (newId !== oldId) {
            this.fetchData()
        }
    }

    static constructAPICacheKeyFromRouteKey(r) {
        var routeKey = JSON.parse(base64url.decode(r, "utf8"));
        return 'filename=' + routeKey.a + '&stackType=' + routeKey.b + '&pid=' + routeKey.c + '&start=' + base64url.encode(routeKey.d, "utf8") + '&end=' + base64url.encode(routeKey.e, "utf8") + '&groupPats=' + base64url.encode(routeKey.f, "utf8") + '&foldPats=' + base64url.encode(routeKey.g, "utf8") + '&incPats=' + base64url.encode(routeKey.h, "utf8") + '&excPats=' + base64url.encode(routeKey.i, "utf8") + '&foldPct=' + routeKey.j + '&drillIntoKey=' + routeKey.k;
    }

    static renderCallersTable(routeKey, callTreeNodeId, node) {
        return (
            <table className="table table-striped table-bordered" id="pd">
                <thead>
                    <tr>
                        <td className="center">Name</td>
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
                    <TreeNode routeKey={routeKey} callTreeNodeId={callTreeNodeId} node={node} indent={0} />
                </tbody>
            </table>
        );
    }

    render() {
        let contents = this.state.loading ? <p><em>Loading...</em></p> : Callers.renderCallersTable(this.props.match.params.routeKey, this.props.match.params.callTreeNodeId, this.state.node);

        return (
            <div style={{margin: 2 + 'px'}}>
                <div style={{margin: 10 + 'px'}}>
                    <h4><Link to={`/ui/hotspots/${this.props.match.params.routeKey}`}>Hotspots</Link> >> Callers</h4>
                    <StackViewerFilter routeKey={this.props.match.params.routeKey}></StackViewerFilter>
                </div>
                {contents}
            </div>
        );
    }
}
