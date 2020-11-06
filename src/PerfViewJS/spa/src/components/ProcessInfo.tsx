import { ModuleList } from './ModuleList';
import { NavMenu } from './NavMenu';
import { ProcessList } from './ProcessList';
import React from 'react';

export interface Props {
    match: any;
}

interface State {
    processInfo: any;
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

interface Thread {
    threadId: number;
    threadIndex: number;
    cpumSec: number;
    startTime: string;
    startTimeRelativeMSec: number;
    endTime: string;
    endTimeRelativeMSec: number;
}

interface Module {
    id: number;
    addrCount: number;
    modulePath: string;
}

interface DetailedProcessInfo {
    processInfo: Process;
    threads: Thread[];
    modules: Module[];
}

export class ProcessInfo extends React.Component<Props, State> {

    static displayName = ProcessInfo.name;

    constructor(props: Props) {
        super(props);
        this.state = { processInfo: null, loading: true };
        fetch('/api/processinfo?filename=' + this.props.match.params.dataFile + '&processIndex=' + this.props.match.params.processIndex, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ processInfo: data, loading: false });
            });
    }

    static render(processInfo: DetailedProcessInfo, dataFile: string) {
        return (
            <div>
                {ProcessList.renderProcessListTable([processInfo.processInfo], dataFile)}
                {ModuleList.renderModuleListTable(processInfo.modules, dataFile)}
                <table className='table table-striped'>
                    <thead>
                        <tr>
                            <th>Thread ID</th>
                            <th>Start Time</th>
                            <th>Start Time Relative MSec</th>
                            <th>End Time</th>
                            <th>End Time Relative MSec</th>
                            <th>CPU Milliseconds</th>
                        </tr>
                    </thead>
                    <tbody>
                        {processInfo.threads.map(thread =>
                            <tr key={`${thread.threadIndex}`}>
                                <td>{thread.threadId}</td>
                                <td>{thread.startTime}</td>
                                <td>{thread.startTimeRelativeMSec}</td>
                                <td>{thread.endTime}</td>
                                <td>{thread.endTimeRelativeMSec}</td>
                                <td>{thread.cpumSec}</td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
        );
    }

    render() {
        let contents = this.state.loading ? <p><em>Loading...</em></p> : ProcessInfo.render(this.state.processInfo, this.props.match.params.dataFile);

        return (
            <div>
                <NavMenu dataFile={this.props.match.params.dataFile} />
                {contents}
            </div>
        );
    }
}
