import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import base64url from 'base64url';

export class ProcessList extends Component {
    static displayName = ProcessList.name;

    constructor(props) {
        super(props);
        this.state = { processes: [], loading: true };
        fetch('/api/processlist?filename=' + this.props.match.params.dataFile + '&stacktype=' + this.props.match.params.stackType, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ processes: data, loading: false });
            });
    }

    static renderProcessListTable(processes, dataFile, stackType) {
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
                            <td><Link to={`/ui/hotspots/${base64url.encode(JSON.stringify({ a: dataFile, b: stackType, c: process.id, d: '', e: '', f: '', g: '', h: process.id === -1 ? '' : 'Process% ' + process.name, i: '', j: '', k: '' }), "utf8")}`}>{process.name}</Link></td>
                            <td>{process.cpumSec}</td>
                        </tr>
                    )}
                </tbody>
            </table>
        );
    }

    render() {
        let contents = this.state.loading ? <p><em>Loading...</em></p> : ProcessList.renderProcessListTable(this.state.processes, this.props.match.params.dataFile, this.props.match.params.stackType);

        return (
            <div>
                <h1>Process List</h1>
                {contents}
            </div>
        );
    }
}
