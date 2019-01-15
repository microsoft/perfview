import React, { PureComponent } from 'react';
import base64url from 'base64url';

export class StackViewerFilter extends PureComponent {
    static displayName = StackViewerFilter.name;

    constructor(props) {
        super(props);
        var data = JSON.parse(base64url.decode(this.props.routeKey, "utf8"));
        this.state = { newRouteKey: this.props.routeKey, start: data.d, end: data.e, groupPats: data.f, foldPats: data.g, incPats: data.h, excPats: data.i, foldPct: data.j };

        this.handleGroupPatsChange = this.handleGroupPatsChange.bind(this);
        this.handleFoldPatsChange = this.handleFoldPatsChange.bind(this);
        this.handleStartChange = this.handleStartChange.bind(this);
        this.handleEndChange = this.handleEndChange.bind(this);
        this.handleIncPatsChange = this.handleIncPatsChange.bind(this);
        this.handleExcPatsChange = this.handleExcPatsChange.bind(this);
        this.handleOnClick = this.handleOnClick.bind(this);
    }

    handleGroupPatsChange(e) {
        this.setState({ groupPats: e.target.value });
    }

    handleFoldPatsChange(e) {
        this.setState({ foldPats: e.target.value });
    }

    handleStartChange(e) {
        this.setState({ start: e.target.value });
    }

    handleEndChange(e) {
        this.setState({ end: e.target.value });
    }

    handleIncPatsChange(e) {
        this.setState({ incPats: e.target.value });
    }

    handleExcPatsChange(e) {
        this.setState({ excPats: e.target.value });
    }

    handleOnClick(e) {

        e.preventDefault();

        var oldRouteKey = JSON.parse(base64url.decode(this.props.routeKey, "utf8"));
        var newRouteKeyJsonString = JSON.stringify({ a: oldRouteKey.a, b: oldRouteKey.b, c: -1, d: this.state.start, e: this.state.end || '', f: this.state.groupPats || '', g: this.state.foldPats || '', h: this.state.incPats || '', i: this.state.excPats || '', j: this.state.foldPct || '' });

        if (JSON.stringify(oldRouteKey) !== newRouteKeyJsonString) {
            window.location.href = '/ui/hotspots/' + base64url.encode(newRouteKeyJsonString, "utf8"); // HACK: But the "react" way is annoying. Any ideas?
        }
    }

    render() {
        return (

            <div>
                <form method="get" className="form-inline">
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
            </div>
        );
    }
}
