import React, { Component } from 'react';
import { Link } from 'react-router-dom'
import { StackViewerFilter } from './StackViewerFilter'
import base64url from 'base64url'

export class Hotspots extends Component {
    static displayName = Hotspots.name;

    constructor(props) {
        super(props);
        this.state = { loading: true, symbolLookupStatus: '' };
        this.handleDrillIntoClick = this.handleDrillIntoClick.bind(this);
        this.handleLookupWarmSymbols = this.handleLookupWarmSymbols.bind(this);
    }

    fetchData() {

        this.setState({ loading: true }); // HACK: Why is this required?

        fetch('/api/hotspots?' + Hotspots.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey), { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                if (!this.ignoreLastFetch) {
                    this.setState({ nodes: data, loading: false });
                }
            });
    }

    handleLookupWarmSymbols() {

        this.setState({ symbolLookupStatus: ' ... performing lookup.' });
        fetch("/api/lookupwarmsymbols?" + Hotspots.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey), { method: 'GET', headers: { 'Content-Type': 'application/json' } })
        .then(res => res.json())
        .then(data => {
            window.location.href = '/ui/hotspots/' + this.props.match.params.routeKey;
        });
    }

    handleDrillIntoClick(d, t) {

        var drillType = '/api/drillinto/exclusive?'
        if (d === 'i') {
            drillType = '/api/drillinto/inclusive?';
        }

        fetch(drillType + Hotspots.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey) + '&name=' + t, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
        .then(res => res.json())
        .then(data => {
            var newRouteKey = JSON.parse(base64url.decode(this.props.match.params.routeKey, "utf8"));
            newRouteKey.k = data;
            window.location.href = '/ui/hotspots/' + base64url.encode(JSON.stringify(newRouteKey));
        });
    }

    componentWillUnmount() {
        this.ignoreLastFetch = true
    }

    componentDidMount() {
        this.fetchData()
    }

    componentDidUpdate(prevProps) {
        let oldId = prevProps.match.params.routeKey
        let newId = this.props.match.params.routeKey
        if (newId !== oldId) {
            this.fetchData()
        }
    }

    static constructAPICacheKeyFromRouteKey(r) {
        var routeKey = JSON.parse(base64url.decode(r, "utf8"));
        return 'filename=' + routeKey.a + '&stackType=' + routeKey.b + '&pid=' + routeKey.c + '&start=' + base64url.encode(routeKey.d, "utf8") + '&end=' + base64url.encode(routeKey.e, "utf8") + '&groupPats=' + base64url.encode(routeKey.f, "utf8") + '&foldPats=' + base64url.encode(routeKey.g, "utf8") + '&incPats=' + base64url.encode(routeKey.h, "utf8") + '&excPats=' + base64url.encode(routeKey.i, "utf8") + '&foldPct=' + routeKey.j + '&drillIntoKey=' + routeKey.k;
    }

    static renderHotspotsTable(nodes, routeKey, obj) {
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
                    {nodes.map(node =>
                        <tr key={`${node.base64EncodedId}`}>
                            <td><Link to={`/ui/callers/${routeKey}/${node.base64EncodedId}`}>{node.name}</Link></td>
                            <td className="center">{node.exclusiveMetricPercent}%</td>
                            <td className="center"><button onClick={() => obj.handleDrillIntoClick('e', node.base64EncodedId)}>{node.exclusiveCount}</button></td>
                            <td className="center">{node.inclusiveMetricPercent}%</td>
                            <td className="center"><button onClick={() => obj.handleDrillIntoClick('i', node.base64EncodedId)}>{node.inclusiveCount}</button></td>
                            <td className="center">{node.exclusiveFoldedMetric}</td>
                            <td className="center">{node.inclusiveMetricByTimeString}</td>
                            <td className="center">{node.firstTimeRelativeMSec}</td>
                            <td className="center">{node.lastTimeRelativeMSec}</td>
                        </tr>
                    )}
                </tbody>
            </table>
        );
    }

    render() {

        let contents = this.state.loading ? <p><em>Loading...</em></p> : Hotspots.renderHotspotsTable(this.state.nodes, this.props.match.params.routeKey, this);

        return (
            <div style={{ margin: 2 + 'px' }}>
                <div style={{ margin: 10 + 'px' }}>
                    <h4>Hotspots</h4>
                    <StackViewerFilter routeKey={this.props.match.params.routeKey}></StackViewerFilter>
                    <button onClick={this.handleLookupWarmSymbols}>Lookup Warm Symbols</button> {this.state.symbolLookupStatus}
                </div>
                {contents}
            </div>
        );
    }
}
