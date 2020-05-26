import { NavMenu } from './NavMenu';
import React from 'react';
import base64url from 'base64url';

export interface Props {
    match: any;
}

interface State {
    events: Event[];
    eventTypes: any;
    traceInfo: any;
    loading: boolean;
    selectedEvents: string;
    start: string;
    end: string;
    textFilter: string;
    maxEventCount: string;
    eventNameFilter: string;
}

interface Event {
    eventId: string;
    name: string;
    rest: string;
    eventIndex: number;
    eventName: string;
    timestamp: string;
    processName: string;
    hasStack: boolean;
}

export class EventViewer extends React.Component<Props, State> {

    static displayName = EventViewer.name;

    myRef: React.RefObject<any>;

    constructor(props: Props) {
        super(props);
        this.myRef = React.createRef();
        this.state = { eventNameFilter: '', traceInfo: null, events: [], eventTypes: [], loading: true, selectedEvents: '', start: '0.000', end: '', textFilter: '', maxEventCount: '1000' };
        this.handleChange = this.handleChange.bind(this);
        this.handleOnClick = this.handleOnClick.bind(this);

        this.handleStartChange = this.handleStartChange.bind(this);
        this.handleEndChange = this.handleEndChange.bind(this);
        this.handleTextFilterChange = this.handleTextFilterChange.bind(this);
        this.handleMaxEventCountChange = this.handleMaxEventCountChange.bind(this);
        this.handleEventTypeFilterList = this.handleEventTypeFilterList.bind(this);

        fetch('/api/traceinfo?filename=' + this.props.match.params.dataFile, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ end: data.endTimeRelativeMSec });
            });

        fetch('/api/eventliston?filename=' + this.props.match.params.dataFile, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ eventTypes: data, loading: false });
            });
    }

    handleOnClick(e: any) {
        e.preventDefault();

        fetch('/api/eventdata?filename=' + this.props.match.params.dataFile + '&maxEventCount=' + this.state.maxEventCount + '&start=' + this.state.start + '&end=' + this.state.end + '&filter=' + base64url.encode(this.state.textFilter, "utf8") + '&eventTypes=' + this.state.selectedEvents, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ events: data });
            });
    }

    handleStartChange(e: any) {
        this.setState({ start: e.target.value });
    }

    handleEndChange(e: any) {
        this.setState({ end: e.target.value });
    }

    handleTextFilterChange(e: any) {
        this.setState({ textFilter: e.target.value });
    }

    handleMaxEventCountChange(e: any) {
        this.setState({ maxEventCount: e.target.value });
    }

    handleChange(e: any) {
        let selectedOptions: HTMLOptionsCollection = e.target.selectedOptions;

        if (this.state.eventTypes.length === selectedOptions.length) {
            this.setState({ selectedEvents: '' });
        }
        else {

            let result: string = '';

            for (let i = 0; i < selectedOptions.length; ++i) {
                result += selectedOptions.item(i)?.value + ',';
            }

            result = result.substring(0, result.length - 1);

            this.setState({ selectedEvents: result });
        }
    }

    handleEventTypeFilterList(e: any) {
        this.setState({ eventNameFilter: e.target.value });
    }

    static renderEventListTable(events: Event[], obj: EventViewer, eventNameFilter: string) {
        return (<React.Fragment>{<select multiple size={events.length} onChange={obj.handleChange}>{events.filter(function f(e) { if (eventNameFilter === '') { return true; } else { return e.eventName.toLowerCase().includes(eventNameFilter.toLowerCase()) } }).map(event => (<option key={event.eventId} value={event.eventId}>{event.eventName}</option>))}</select>}</React.Fragment>);
    }

    render() {

        let contents = this.state.loading ? <p><em>Loading...</em></p> : EventViewer.renderEventListTable(this.state.eventTypes, this, this.state.eventNameFilter);

        return (
            <div>
                <NavMenu dataFile={this.props.match.params.dataFile} />
                <div style={{ paddingLeft: 3 + 'px' }}>
                    <h1>Event Viewer</h1>
                    <div>
                        <form style={{ fontSize: 9 + 'pt' }} method="get" className="form-inline">
                            <div >
                                <label htmlFor="StartBox">Start Time (Relative MSec)</label>
                                <input type="text" name="start" id="StartBox" value={`${this.state.start}`} onChange={this.handleStartChange} />
                                <label htmlFor="TextFilterBox">Text Filter (.NET Regex)</label>
                                <input type="text" name="textfilter" id="TextFilterBox" value={`${this.state.textFilter}`} onChange={this.handleTextFilterChange} />
                            </div>
                            <div style={{ marginLeft: 10 + 'px' }}>
                                <label htmlFor="EndBox">End Time (Relative MSec)</label>
                                <input type="text" name="end" id="EndBox" value={`${this.state.end}`} onChange={this.handleEndChange} />
                                <label htmlFor="MaxEventCountBox">Maximum Event Count</label>
                                <input type="text" name="maxEventCount" id="MaxEventCountBox" value={`${this.state.maxEventCount}`} onChange={this.handleMaxEventCountChange} />
                            </div>
                            <div style={{ marginLeft: 10 + 'px' }}>
                                <button onClick={this.handleOnClick} className="btn btn-primary">Update</button>
                            </div>
                        </form>
                    </div>
                    <div style={{ paddingTop: 10 + 'px' }}>Event Type Filter: <input type="text" onChange={this.handleEventTypeFilterList}></input></div>
                    <div style={{ paddingTop: 10 + 'px', fontSize: 9 + 'pt', whiteSpace: 'nowrap', height: 100 + '%', width: 100 + '%' }}>
                        <div style={{ overflow: 'auto', float: 'left', width: 350 + 'px' }}>
                            {contents}
                        </div>
                        <div style={{ overflow: 'auto' }}>
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
                                            <td>{event.hasStack ? <a target="_blank" rel="noopener noreferrer" href={`/ui/stackviewer/hotspots/${base64url.encode(JSON.stringify({ a: this.props.match.params.dataFile, b: '-1', c: '-1', d: (parseFloat(event.timestamp) - 0.001).toFixed(3), e: (parseFloat(event.timestamp) + 0.001).toFixed(3), f: '', g: '', h: '', i: '', j: '', k: '', l: base64url.encode('Any Event', 'utf8') }), "utf8")}`}>HasStack="True"</a> : ''} {event.rest}</td>
                                        </tr>
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </div>
        );
    }
}
