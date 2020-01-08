import { Link } from 'react-router-dom';
import { NavMenu } from './NavMenu';
import React from 'react';
import base64url from 'base64url';

export interface Props {
    match: any;
}

interface State {
    processes: any;
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
        fetch('/api/processchooser?filename=' + this.props.match.params.dataFile + '&stacktype=' + this.props.match.params.stackType, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ processes: data, loading: false });
            });
    }

    static renderProcessChooserTable(processes: Process[], dataFile: string, stackType: number, stackTypeName: string) {
        return (
            <table className='table table-striped'>
                <thead>
                    <tr>
                        <th>Process Name</th>
                        <th>CPU MSec</th>
                    </tr>
                </thead>
                <tbody>
                    {processes.map(process =>
                        <tr key={`${process.id}`}>
                            <td><Link to={`/ui/stackviewer/hotspots/${base64url.encode(JSON.stringify({ a: dataFile, b: stackType, c: process.id, d: '', e: '', f: '', g: '', h: process.id === -1 ? '' : 'Process% ' + process.name, i: '', j: '', k: '', l: stackTypeName }), "utf8")}`}>{process.name}</Link></td>
                            <td>{process.cpumSec}</td>
                        </tr>
                    )}
                </tbody>
            </table>
        );
    }

    render() {
        let contents = this.state.loading ? <p><em>Loading...</em></p> : ProcessChooser.renderProcessChooserTable(this.state.processes, this.props.match.params.dataFile, this.props.match.params.stackType, this.props.match.params.stackTypeName);

        return (
            <div>
                <NavMenu dataFile={this.props.match.params.dataFile} />
                <h4>Event {base64url.decode(this.props.match.params.stackTypeName, "utf8")} &raquo; Choose Process</h4>
                {contents}
            </div>
        );
    }
}
