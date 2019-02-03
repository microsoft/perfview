import React, { Component } from 'react';
import { Link } from 'react-router-dom'

export class EventList extends Component {
    static displayName = EventList.name;

    constructor(props) {
        super(props);
        var dataFile = this.props.match.params.dataFile;
        this.state = { dataFile: dataFile, events: [], loading: true };
        fetch('/api/eventlist?filename=' + dataFile, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ events: data, loading: false });
            });
    }

    static renderEventListTable(events, dataFile) {
        return (
            <table className='table table-striped'>
                <thead>
                    <tr>
                        <th>Event Name</th>
                        <th>Stack Count</th>
                        <th>Event Count</th>
                    </tr>
                </thead>
                <tbody>
                    {events.map(event =>
                        <tr key={`${event.name} (${event.eventId}`}>
                            <td>{event.stackEventCount !== 0 ? <Link to={`/ui/processlist/${dataFile}/${event.eventId}`}>{event.eventName}</Link> : event.eventName}</td>
                            <td>{event.stackEventCount}</td>
                            <td>{event.eventCount}</td>
                        </tr>
                    )}
                </tbody>
            </table>
        );
    }

    render() {

        if (this.state.error) {
            return (<div>{this.state.error}</div>)
        }

        let contents = this.state.loading ? <p><em>Loading...</em></p> : EventList.renderEventListTable(this.state.events, this.state.dataFile);

        return (
            <div>
                <h1>BPerf</h1>
                {contents}
            </div>
        );
    }
}
