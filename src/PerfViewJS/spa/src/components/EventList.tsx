import { Link } from 'react-router-dom'
import { NavMenu } from './NavMenu';
import React from 'react';
import base64url from 'base64url'

export interface Props {
    match: any;
}

interface State {
    dataFile: string;
    events: Event[];
    loading: boolean;
    error: boolean;
}

interface Event {
    stackEventCount: number;
    eventId: string;
    name: string;
    eventCount: string;
    eventName: string;
}

export class EventList extends React.Component<Props, State> {
    static displayName = EventList.name;

    constructor(props: Props) {
        super(props);
        var dataFile = this.props.match.params.dataFile;
        this.state = { dataFile: dataFile, events: [], loading: true, error: false };
        fetch('/api/eventlistos?filename=' + dataFile, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ events: data, loading: false });
            });
    }

    static renderEventListTable(events: Event[], dataFile: string) {
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
                            <td>{event.stackEventCount !== 0 ? <Link to={`/ui/stackviewer/processchooser/${dataFile}/${event.eventId}/${base64url.encode(event.eventName, "utf8")}`}>{event.eventName}</Link> : event.eventName}</td>
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
                <NavMenu dataFile={this.state.dataFile} />
                <h4>Choose Stack Type</h4>
                {contents}
            </div>
        );
    }
}
