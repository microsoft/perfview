import React from 'react';
import base64url from 'base64url';

export interface Props {
    routeKey: string;
}

interface State {
    newRouteKey: string;
    start: string;
    end: string;
    groupPats: string;
    foldPats: string;
    incPats: string;
    excPats: string;
    foldPct: string;
    minCount: number;
    symbolLookupStatus: string;
    symbolLog: string;
}

export class StackViewerFilter extends React.PureComponent<Props, State> {

    static displayName = StackViewerFilter.name;

    constructor(props: Props) {
        super(props);
        var data = JSON.parse(base64url.decode(this.props.routeKey, "utf8"));
        this.state = { symbolLog: '', symbolLookupStatus: '', minCount: 50, newRouteKey: this.props.routeKey, start: data.d, end: data.e, groupPats: data.f, foldPats: data.g, incPats: data.h, excPats: data.i, foldPct: data.j };

        this.handleGroupPatsChange = this.handleGroupPatsChange.bind(this);
        this.handleFoldPatsChange = this.handleFoldPatsChange.bind(this);
        this.handleStartChange = this.handleStartChange.bind(this);
        this.handleEndChange = this.handleEndChange.bind(this);
        this.handleIncPatsChange = this.handleIncPatsChange.bind(this);
        this.handleExcPatsChange = this.handleExcPatsChange.bind(this);
        this.handleOnClick = this.handleOnClick.bind(this);
        this.handleLookupWarmSymbols = this.handleLookupWarmSymbols.bind(this);
        this.handleLookupWarmSymbolsMinCount = this.handleLookupWarmSymbolsMinCount.bind(this);
    }

    handleGroupPatsChange(e: any) {
        this.setState({ groupPats: e.target.value });
    }

    handleFoldPatsChange(e: any) {
        this.setState({ foldPats: e.target.value });
    }

    handleStartChange(e: any) {
        this.setState({ start: e.target.value });
    }

    handleEndChange(e: any) {
        this.setState({ end: e.target.value });
    }

    handleIncPatsChange(e: any) {
        this.setState({ incPats: e.target.value });
    }

    handleExcPatsChange(e: any) {
        this.setState({ excPats: e.target.value });
    }

    handleOnClick(e: any) {

        e.preventDefault();

        var oldRouteKey = JSON.parse(base64url.decode(this.props.routeKey, "utf8"));
        var newRouteKeyJsonString = JSON.stringify({ a: oldRouteKey.a, b: oldRouteKey.b, c: -1, d: this.state.start, e: this.state.end || '', f: this.state.groupPats || '', g: this.state.foldPats || '', h: this.state.incPats || '', i: this.state.excPats || '', j: this.state.foldPct || '', l: oldRouteKey.l });

        if (JSON.stringify(oldRouteKey) !== newRouteKeyJsonString) {
            window.location.href = '/ui/stackviewer/hotspots/' + base64url.encode(newRouteKeyJsonString, "utf8"); // HACK: But the "react" way is annoying. Any ideas?
        }
    }

    handleLookupWarmSymbolsMinCount(e: any) {
        this.setState({ minCount: e.target.value });
    }

    handleLookupWarmSymbols() {

        this.setState({ symbolLookupStatus: ' ... performing lookup.' });
        fetch("/api/lookupwarmsymbols?minCount=" + this.state.minCount + "&" + StackViewerFilter.constructAPICacheKeyFromRouteKey(this.props.routeKey), { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                window.location.href = '/ui/stackviewer/hotspots/' + this.props.routeKey;
            });
    }

    static constructAPICacheKeyFromRouteKey(r: string) {
        var routeKey = JSON.parse(base64url.decode(r, "utf8"));
        return 'filename=' + routeKey.a + '&stackType=' + routeKey.b + '&pid=' + routeKey.c + '&start=' + base64url.encode(routeKey.d, "utf8") + '&end=' + base64url.encode(routeKey.e, "utf8") + '&groupPats=' + base64url.encode(routeKey.f, "utf8") + '&foldPats=' + base64url.encode(routeKey.g, "utf8") + '&incPats=' + base64url.encode(routeKey.h, "utf8") + '&excPats=' + base64url.encode(routeKey.i, "utf8") + '&foldPct=' + routeKey.j + '&drillIntoKey=' + routeKey.k;
    }

    render() {
        return (
            <div>
                <div style={{ float: 'left' }}>
                    <form style={{ fontSize: 9 + 'pt' }} method="get" className="form-inline">
                        <div>
                            <label htmlFor="GroupPatsBox">Grouping Patterns (Regex)</label>
                            <input type="text" name="GroupPats" className="form-control" id="GroupPatsBox" aria-describedby="Start Time" value={`${this.state.groupPats}`} onChange={this.handleGroupPatsChange} />
                            <label htmlFor="FoldPatsBox">Folding Patterns (Regex)</label>
                            <input type="text" name="FoldPats" className="form-control" id="FoldPatsBox" aria-describedby="End Time" value={`${this.state.foldPats}`} onChange={this.handleFoldPatsChange} />
                        </div>
                        <div style={{ marginLeft: 10 + 'px' }}>
                            <label htmlFor="Start">Relative Start Time (ms)</label>
                            <input type="text" className="form-control" id="Start" name="Start" aria-describedby="Start Time" value={`${this.state.start}`} onChange={this.handleStartChange} />
                            <label htmlFor="End">Relative End Time (ms)</label>
                            <input type="text" className="form-control" id="End" name="End" aria-describedby="End Time" value={`${this.state.end}`} onChange={this.handleEndChange} />
                        </div>
                        <div style={{ marginLeft: 10 + 'px' }}>
                            <label htmlFor="IncPatsBox">Include Patterns (Regex)</label>
                            <input type="text" name="IncPats" className="form-control" id="IncPatsBox" aria-describedby="Start Time" value={`${this.state.incPats}`} onChange={this.handleIncPatsChange} />
                            <label htmlFor="ExcPatsBox">Exclude Patterns (Regex)</label>
                            <input type="text" name="ExcPats" className="form-control" id="ExcPatsBox" aria-describedby="End Time" value={`${this.state.excPats}`} onChange={this.handleExcPatsChange} />
                        </div>

                        <div style={{ marginLeft: 10 + 'px' }}>
                            <button onClick={this.handleOnClick} className="btn btn-primary">Update</button>
                        </div>
                    </form>
                    <div style={{ paddingTop: 10 + 'px' }}>
                        <button onClick={this.handleLookupWarmSymbols}>Lookup Symbols (min # of samples) &raquo; </button>
                        <input type="text" value={this.state.minCount} onChange={this.handleLookupWarmSymbolsMinCount} /> {this.state.symbolLookupStatus}
                    </div>
                </div>
                <div style={{ fontSize: 9 + 'pt', float: 'left', paddingLeft: 20 + 'px' }}>
                    <strong>Grouping Patterns Examples</strong>
                    <table>
                        <thead>
                            <tr><td>Pattern</td><td>Comment</td></tr>
                        </thead>
                        <tbody>
                            <tr><td>{`{`}%}!-&gt;module $1</td><td><strong>Group Modules - Provides high-level overview (i.e. per dll/module cost)</strong></td></tr>
                            <tr><td>{`{`}*}!=&gt;module $1</td><td>Group Full Path Module Entries</td></tr>
                            <tr><td>{`{`}%}!=&gt;module $1</td><td>Group Module Entries</td></tr>
                            <tr><td>{`{`}%!*}.%(-&gt;class $1;{`{`}%!*}::-&gt;class $1</td><td>Group Classes</td></tr>
                            <tr><td>{`{`}%!*}.%(=&gt;class $1;{`{`}%!*}::=&gt;class $1</td><td>Group Class Entries</td></tr>
                            <tr><td>Thread -&gt; AllThreads</td><td>Fold Threads</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>
        );
    }
}