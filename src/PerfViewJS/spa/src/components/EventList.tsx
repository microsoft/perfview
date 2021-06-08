import { Link } from "react-router-dom";
import React from "react";
import base64url from "base64url";
import { RouteComponentProps } from "react-router";

interface MatchParams {
  dataFile: string;
}
export interface Props extends RouteComponentProps<MatchParams> {}

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
    const dataFile = this.props.match.params.dataFile;
    this.state = {
      dataFile: dataFile,
      events: [],
      loading: true,
      error: false,
    };
    fetch("/api/eventlistos?filename=" + dataFile, {
      method: "GET",
      headers: { "Content-Type": "application/json" },
    })
      .then((res) => res.json())
      .then((data) => {
        this.setState({ events: data, loading: false });
      });
  }

  static renderEventListTable(events: Event[], dataFile: string) {
    return (
      <table className="table table-striped">
        <thead>
          <tr>
            <th>Event Name</th>
            <th>Stack Count</th>
            <th>Event Count</th>
          </tr>
        </thead>
        <tbody>
          {events.map((event) => (
            <tr key={`${event.name} (${event.eventId}`}>
              <td>
                {event.stackEventCount !== 0 ? (
                  <Link
                    to={`/ui/stackviewer/processchooser/${dataFile}/${event.eventId}/${base64url.encode(
                      event.eventName
                    )}`}
                  >
                    {event.eventName}
                  </Link>
                ) : (
                  event.eventName
                )}
              </td>
              <td>{event.stackEventCount}</td>
              <td>{event.eventCount}</td>
            </tr>
          ))}
        </tbody>
      </table>
    );
  }

  render() {
    if (this.state.error) {
      return <div>{this.state.error}</div>;
    }

    const contents = this.state.loading ? (
      <p>
        <em>Loading...</em>
      </p>
    ) : (
      EventList.renderEventListTable(this.state.events, this.state.dataFile)
    );

    return (
      <div>
        <h4>Choose Stack Type</h4>
        {contents}
      </div>
    );
  }
}
