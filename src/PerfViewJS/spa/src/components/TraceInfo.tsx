import React from "react";
import { RouteComponentProps } from "react-router";

interface MatchParams {
  dataFile: string;
}

export interface Props extends RouteComponentProps<MatchParams> { }

interface State {
  traceInfo: TraceInfoInterface | null;
  loading: boolean;
}

interface TraceInfoInterface {
  machineName: string;
  operatingSystemName: string;
  operatingSystemBuildNumber: string;
  utcDiff: number;
  utcOffsetCurrentProcess: number;
  bootTime: string;
  startTime: string;
  endTime: string;
  duration: number;
  processorSpeed: number;
  numberOfProcessors: number;
  memorySize: number;
  pointerSize: number;
  sampleProfileInterval: number;
  totalEvents: number;
  lostEvents: number;
  fileSize: number;
}

export class TraceInfo extends React.Component<Props, State> {
  static displayName = TraceInfo.name;

  constructor(props: Props) {
    super(props);
    this.state = { loading: true, traceInfo: null };
    fetch("/api/traceinfo?filename=" + this.props.match.params.dataFile, {
      method: "GET",
      headers: { "Content-Type": "application/json" },
    })
      .then((res) => res.json())
      .then((data) => {
        this.setState({ traceInfo: data, loading: false });
      });
  }

  static renderTraceInfoTable(traceInfo: TraceInfoInterface) {
    return (
      <table className="table table-striped">
        <thead>
          <tr>
            <th>Info Type</th>
            <th>Info Value</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Machine Name</td>
            <td>{traceInfo.machineName}</td>
          </tr>
          <tr>
            <td>Operating System</td>
            <td>{traceInfo.operatingSystemName}</td>
          </tr>
          <tr>
            <td>OS Build Number</td>
            <td>{traceInfo.operatingSystemBuildNumber}</td>
          </tr>
          <tr>
            <td>UTC Diff</td>
            <td>{traceInfo.utcDiff}</td>
          </tr>
          <tr>
            <td>Current UTC (of this tool)</td>
            <td>{traceInfo.utcOffsetCurrentProcess}</td>
          </tr>
          <tr>
            <td>OS Boot Time</td>
            <td>{traceInfo.bootTime}</td>
          </tr>
          <tr>
            <td>Trace Start Time</td>
            <td>{traceInfo.startTime}</td>
          </tr>
          <tr>
            <td>Trace End Time</td>
            <td>{traceInfo.endTime}</td>
          </tr>
          <tr>
            <td>Trace Duration (Sec)</td>
            <td>{traceInfo.duration}</td>
          </tr>
          <tr>
            <td>CPU Frequency (MHz)</td>
            <td>{traceInfo.processorSpeed}</td>
          </tr>
          <tr>
            <td>Number Of Processors</td>
            <td>{traceInfo.numberOfProcessors}</td>
          </tr>
          <tr>
            <td>Memory Size</td>
            <td>{traceInfo.memorySize}</td>
          </tr>
          <tr>
            <td>Sample Profile Interval (MSec)</td>
            <td>{traceInfo.sampleProfileInterval}</td>
          </tr>
          <tr>
            <td>Total Events</td>
            <td>{traceInfo.totalEvents}</td>
          </tr>
          <tr>
            <td>Lost Events</td>
            <td>{traceInfo.lostEvents}</td>
          </tr>
          <tr>
            <td>File Size (MB)</td>
            <td>{traceInfo.fileSize}</td>
          </tr>
        </tbody>
      </table>
    );
  }

  render() {
    const contents = this.state.loading ? (
      <p>
        <em>Loading...</em>
      </p>
    ) : this.state.traceInfo != null ? (
      TraceInfo.renderTraceInfoTable(this.state.traceInfo)
    ) : (
      "Null Data"
    );

    return (
      <div>
        <h1>Trace Info</h1>
        {contents}
      </div>
    );
  }
}
