import React from "react";
import { RouteComponentProps } from "react-router-dom";

interface MatchParams {
  dataFile: string;
}

export interface Props extends RouteComponentProps<MatchParams> {}

interface State {
  modules: Module[];
  loading: boolean;
}

interface Module {
  modulePath: string;
  id: number;
  addrCount: number;
}

export class ModuleList extends React.Component<Props, State> {
  static displayName = ModuleList.name;

  constructor(props: Props) {
    super(props);
    this.state = { modules: [], loading: true };
    fetch("/api/modulelist?filename=" + this.props.match.params.dataFile, {
      method: "GET",
      headers: { "Content-Type": "application/json" },
    })
      .then((res) => res.json())
      .then((data) => {
        this.setState({ modules: data, loading: false });
      });
  }

  static renderModuleListTable(modules: Module[], dataFile: string) {
    return (
      <table className="table table-striped">
        <thead>
          <tr>
            <th>Module Name</th>
            <th>Number of address occurrences in all stacks</th>
          </tr>
        </thead>
        <tbody>
          {modules.map((module) => (
            <tr key={`${module.id}`}>
              <td>{module.modulePath}</td>
              <td>
                <a target="_blank" rel="noopener noreferrer" href={`/api/lookupsymbol?filename=${dataFile}&moduleIndex=${module.id}`}>
                  {module.addrCount}
                </a>
              </td>
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
      ModuleList.renderModuleListTable(this.state.modules, this.props.match.params.dataFile)
    );

    return (
      <div>
        <h1>Module List</h1>
        {contents}
      </div>
    );
  }
}
