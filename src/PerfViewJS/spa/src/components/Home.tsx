import React from 'react';
import { Redirect } from 'react-router';
import base64url from 'base64url'

export interface Props {
    match: any;
}

interface State {
    dataFile: string;
    startTime: string;
    endTime: string;
    redirect: boolean;
    files: string[];
}

export class Home extends React.Component<Props, State> {

    static displayName = Home.name;

    constructor(props: any) {
        super(props);
        this.state = { files: [], dataFile: "", startTime: "", endTime: "", redirect: false };
        this.handleDataFileChange = this.handleDataFileChange.bind(this);
        this.handleStartTimeChange = this.handleStartTimeChange.bind(this);
        this.handleEndTimeChange = this.handleEndTimeChange.bind(this);
        this.handleOnClick = this.handleOnClick.bind(this);

        fetch('/api/datadirectorylisting', { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ files: data });
            });
    }

    handleDataFileChange(e: string) {
        this.setState({ dataFile: e });
    }

    handleStartTimeChange(e: any) {
        this.setState({ startTime: e.target.value });
    }

    handleEndTimeChange(e: any) {
        this.setState({ endTime: e.target.value });
    }

    handleOnClick() {
        this.setState({ redirect: true });
    }

    render() {

        if (this.state.redirect) {
            var encoded = base64url.encode(this.state.dataFile + "*" + this.state.startTime + "*" + this.state.endTime, "utf8");
            return <Redirect push to={`/ui/eventviewer/${(encoded)}`} />;
        }

        return (
            <div>
                <h1>PerfViewJS</h1>

                File Path: <input type="dataFile" value={this.state.dataFile} readOnly />
                Start Time: <input type="startTime" onChange={this.handleStartTimeChange} />
                End Time: <input type="endTime" onChange={this.handleEndTimeChange} />

                <button className="btn btn-secondary" onClick={this.handleOnClick}>Analyze</button>

                <table className="table table-striped table-bordered" id="pd">
                    <thead>
                        <tr>
                            <td><h5>Choose a File (it populates the above form)</h5></td>
                        </tr>
                    </thead>
                    <tbody>
                        {this.state.files.map(file =>
                            <tr key={`${file}`}>
                                <td><button className="btn btn-secondary btn-sm" onClick={() => this.handleDataFileChange(file)}>{file}</button></td>
                            </tr>)}
                    </tbody>
                </table>
            </div>
        );
    }
}
