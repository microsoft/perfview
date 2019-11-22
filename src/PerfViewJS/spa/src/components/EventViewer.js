import React, { Component } from 'react';
import base64url from 'base64url';

export class EventViewer extends Component {
    static displayName = EventViewer.name;

    constructor(props) {
        super(props);
        this.myRef = React.createRef();
        this.state = { events: [], eventTypes: [], loading: true, selectedEvents: new Map(), start: '', end: '', textFilter: '', maxEventCount: '' };
        this.handleChange = this.handleChange.bind(this);
        this.handleOnClick = this.handleOnClick.bind(this);

        this.handleStartChange = this.handleStartChange.bind(this);
        this.handleEndChange = this.handleEndChange.bind(this);
        this.handleTextFilterChange = this.handleTextFilterChange.bind(this);
        this.handleMaxEventCountChange = this.handleMaxEventCountChange.bind(this);

        fetch('/api/eventlist?filename=' + this.props.match.params.dataFile, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ eventTypes: data, loading: false });
            });
    }

    handleOnClick(e) {
        e.preventDefault();

        var result = '';
        this.state.selectedEvents.forEach((m, k, v) => {
            if (m) {
                result += k + ',';
            }
        });

        result = result.substring(0, result.length - 1);

        fetch('/api/eventdata?filename=' + this.props.match.params.dataFile + '&maxEventCount=' + this.state.maxEventCount + '&start=' + this.state.start + '&end=' + this.state.end + '&filter=' + base64url.encode(this.state.textFilter, "utf8") + '&eventTypes=' + result, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ events: data });
            });
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

    handleChange(e) {
        const item = e.target.name;
        const isChecked = e.target.checked;
        this.setState(prevState => ({ selectedEvents: prevState.selectedEvents.set(item, isChecked) }));
    }

    static renderEventListTable(events, selectedEvents, obj) {
        return (
            <React.Fragment>
                {
                    events.map(event => (
                        <div key={`${event.name} (${event.eventId}`}>
                            <label>
                                <input type='checkbox' name={event.eventId} checked={selectedEvents.get(event.eventId)} onChange={obj.handleChange} /> {event.eventName}
                            </label>
                        </div>
                    ))
                }
            </React.Fragment>
        );
    }

    render() {

        let contents = this.state.loading ? <p><em>Loading...</em></p> : EventViewer.renderEventListTable(this.state.eventTypes, this.state.selectedEvents, this);

        return (
            <div>
                <h1>Event Viewer</h1>
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
                <div style={{ width: 100 + '%' }}>
                    <div style={{ float: 'left', width: 10 + '%' }}>{contents}</div>
                    <div style={{ float: 'left', width: 90 + '%' }}>
                        <table className='table table-striped'>
                            <thead>
                                <tr>
                                    <th>Event Name</th>
                                    <th>Time MSec</th>
                                    <th>Process Name</th>
                                    <th>Rest</th>
                                </tr>
                            </thead>
                            <tbody>
                                {this.state.events.map(event =>
                                    <tr key={event.eventIndex}>
                                        <td>{event.eventName}</td>
                                        <td>{event.timestamp}</td>
                                        <td>{event.processName}</td>
                                        <td>{event.hasStack ? <a href={`/ui/callstackview/${event.eventIndex}`}>HasStack="True"</a> : ''} {event.rest}</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        );
    }
}
