import { Link } from 'react-router-dom'
import { NavMenu } from './NavMenu';
import React from 'react';
import { StackViewerFilter } from './StackViewerFilter'
import { TNode } from './TNode';
import base64url from 'base64url'

export interface Props {
    match: any;
}

interface State {
    loading: boolean;
    nodes: TNode[];
}

export class Hotspots extends React.Component<Props, State> {

    ignoreLastFetch: boolean;

    static displayName = Hotspots.name;

    constructor(props: Props) {
        super(props);
        this.ignoreLastFetch = false;
        this.state = { loading: true, nodes: [] };
        this.handleDrillIntoClick = this.handleDrillIntoClick.bind(this);
    }

    fetchData() {

        this.setState({ loading: true }); // HACK: Why is this required?

        fetch('/api/hotspots?' + StackViewerFilter.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey), { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                if (!this.ignoreLastFetch) {
                    this.setState({ nodes: data, loading: false });
                }
            });
    }

    handleDrillIntoClick(d: string, t: string) {

        var drillType = '/api/drillinto/exclusive?'
        if (d === 'i') {
            drillType = '/api/drillinto/inclusive?';
        }

        fetch(drillType + StackViewerFilter.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey) + '&name=' + t, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                var newRouteKey = JSON.parse(base64url.decode(this.props.match.params.routeKey, "utf8"));
                newRouteKey.k = data;
                window.location.href = '/ui/stackviewer/hotspots/' + base64url.encode(JSON.stringify(newRouteKey));
            });
    }

    componentWillUnmount() {
        this.ignoreLastFetch = true
    }

    componentDidMount() {
        this.fetchData()
    }

    componentDidUpdate(prevProps: Props) {
        let oldId = prevProps.match.params.routeKey
        let newId = this.props.match.params.routeKey
        if (newId !== oldId) {
            this.fetchData()
        }
    }

    static renderHotspotsTable(nodes: TNode[], routeKey: string, obj: Hotspots) {
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
                            <td><Link to={`/ui/stackviewer/callers/${routeKey}/${node.base64EncodedId}`}>{node.name}</Link></td>
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
            <div>
                <NavMenu dataFile={JSON.parse(base64url.decode(this.props.match.params.routeKey, "utf8")).a} />
                <div style={{ margin: 2 + 'px' }}>
                    <div style={{ margin: 10 + 'px' }}>
                        <h4>{base64url.decode(JSON.parse(base64url.decode(this.props.match.params.routeKey, "utf8")).l, "utf8")} >> Hotspots</h4>
                        <StackViewerFilter routeKey={this.props.match.params.routeKey}></StackViewerFilter>
                    </div>
                    {contents}
                </div>
            </div>
        );
    }
}
