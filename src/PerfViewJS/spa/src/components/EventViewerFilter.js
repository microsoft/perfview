import React, { PureComponent } from 'react';
import base64url from 'base64url';

export class EventViewerFilter extends PureComponent {
    static displayName = EventViewerFilter.name;

    constructor(props) {
        super(props);
        var data = JSON.parse(base64url.decode(this.props.routeKey, "utf8"));
        this.state = { newRouteKey: this.props.routeKey, start: data.d, end: data.e, groupPats: data.f, foldPats: data.g, incPats: data.h, excPats: data.i, foldPct: data.j };

        this.handleStartChange = this.handleStartChange.bind(this);
        this.handleEndChange = this.handleEndChange.bind(this);
        this.handleTextFilterChange = this.handleTextFilterChange.bind(this);
        this.handleMaxEventCountChange = this.handleMaxEventCountChange.bind(this);
    }

    handleStartChange(e) {
        this.setState({ start: e.target.value });
    }

    handleEndChange(e) {
        this.setState({ end: e.target.value });
    }

    handleTextFilterChange(e) {
        this.setState({ textFilter: e.target.value });
    }

    handleMaxEventCountChange(e) {
        this.setState({ maxEventCount: e.target.value });
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
                        <label htmlFor="StartBox">Start Time (Relative MSec)</label>
                        <input type="text" name="start" className="form-control" id="StartBox" value={`${this.state.start}`} onChange={this.handleStartChange} />
                        <label htmlFor="TextFilterBox">Text Filter (.NET Regex)</label>
                        <input type="text" name="textfilter" className="form-control" id="TextFilterBox" value={`${this.state.textFilter}`} onChange={this.handleTextFilterChange} />
                    </div>
                    <div style={{ marginLeft: 10 + 'px' }}>
                        <label htmlFor="EndBox">End Time (Relative MSec)</label>
                        <input type="text" name="end" className="form-control" id="EndBox" value={`${this.state.end}`} onChange={this.handleEndChange} />
                        <label htmlFor="MaxEventCountBox">Maximum Event Count</label>
                        <input type="text" name="maxEventCount" className="form-control" id="MaxEventCountBox" value={`${this.state.maxEventCount}`} onChange={this.handleMaxEventCountChange} />
                    </div>
                    <div style={{ marginLeft: 10 + 'px' }}>
                        <button onClick={this.handleOnClick} className="btn btn-primary">Update</button>
                    </div>
                </form>
            </div>
        );
    }
}
