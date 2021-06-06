import { Link, RouteComponentProps } from "react-router-dom";
import React from "react";
import base64url from "base64url";
interface MatchParams {
  dataFile: string;
  stackType: string;
  stackTypeName: string;
}

export interface Props extends RouteComponentProps<MatchParams> {}

interface State {
  processes: Process[];
  loading: boolean;
}

interface Process {
  name: string;
  id: number;
  cpumSec: number;
}

export class ProcessChooser extends React.Component<Props, State> {
  static displayName = ProcessChooser.name;

  constructor(props: Props) {
    super(props);
    this.state = { processes: [], loading: true };
    fetch(
      "/api/processchooser?filename=" +
        this.props.match.params.dataFile +
        "&stacktype=" +
        this.props.match.params.stackType,
      { method: "GET", headers: { "Content-Type": "application/json" } }
    )
      .then((res) => res.json())
      .then((data) => {
        this.setState({ processes: data, loading: false });
      });
  }

  static renderProcessChooserTable(processes: Process[], dataFile: string, stackType: number, stackTypeName: string) {
    return (
      <table className="table table-striped">
        <thead>
          <tr>
            <th>Process Name</th>
            <th>CPU MSec</th>
          </tr>
        </thead>
        <tbody>
          {processes.map((process) => (
            <tr key={`${process.id}`}>
              <td>
                <Link
                  to={`/ui/stackviewer/hotspotsOld/${base64url.encode(
                    JSON.stringify({
                      a: dataFile,
                      b: stackType,
                      c: process.id,
                      d: "",
                      e: "",
                      f: "",
                      g: "",
                      h: process.id === -1 ? "" : "Process% " + process.name,
                      i: "",
                      j: "",
                      k: "",
                      l: stackTypeName,
                    }),
                    "utf8"
                  )}`}
                >
                  {process.name}
                </Link>
              </td>
              <td>{process.cpumSec}</td>
            </tr>
          ))}
        </tbody>
      </table>
    );
  }

  render() {
    const contents = this.state.loading ? (
      <p>
        <em>Loading...</em>
      </p>
    ) : (
      ProcessChooser.renderProcessChooserTable(
        this.state.processes,
        this.props.match.params.dataFile,
        parseInt(this.props.match.params.stackType, 10),
        this.props.match.params.stackTypeName
      )
    );

    return (
      <div>
        <h4>Event {base64url.decode(this.props.match.params.stackTypeName, "utf8")} &raquo; Choose Process</h4>
        {contents}
      </div>
    );
  }
}
