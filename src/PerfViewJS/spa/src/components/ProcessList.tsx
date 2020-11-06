import { Link } from 'react-router-dom';
import { NavMenu } from './NavMenu';
import React from 'react';

export interface Props {
    match: any;
}

interface State {
    processes: any;
    loading: boolean;
}

interface Process {
    id: number;
    processId: number;
    parentId: number;
    name: string;
    commandLine: string;
    cpumSec: number;
}

export class ProcessList extends React.Component<Props, State> {

    static displayName = ProcessList.name;

    constructor(props: Props) {
        super(props);
        this.state = { processes: [], loading: true };
        fetch('/api/processchooser?filename=' + this.props.match.params.dataFile, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ processes: data, loading: false });
            });
    }

    static renderProcessListTable(processes: Process[], dataFile: string) {
        return (
            <table className='table table-striped'>
                <thead>
                    <tr>
                        <th>Process Name</th>
                        <th>Process Id</th>
                        <th>Parent Id</th>
                        <th>CPU Milliseconds</th>
                        <th>Command Line</th>
                    </tr>
                </thead>
                <tbody>
                    {processes.filter(function (p) { return p.id !== -1 }).map(process =>
                        <tr key={`${process.id}`}>
                            <td>{process.processId === 0 ? "Idle" : process.processId === 4 ? "System" : (<Link to={`/ui/processInfo/${dataFile}/${process.id}`}>{process.name}</Link>)}</td>
                            <td>{process.processId}</td>
                            <td>{process.parentId}</td>
                            <td>{process.cpumSec}</td>
                            <td>{process.commandLine}</td>
                        </tr>
                    )}
                </tbody>
            </table>
        );
    }

    render() {
        let contents = this.state.loading ? <p><em>Loading...</em></p> : ProcessList.renderProcessListTable(this.state.processes, this.props.match.params.dataFile);

        return (
            <div>
                <NavMenu dataFile={this.props.match.params.dataFile} />
                <h1>Process List</h1>
                {contents}
            </div>
        );
    }
}
