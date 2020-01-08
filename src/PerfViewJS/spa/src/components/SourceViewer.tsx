import './SourceViewer.css';

import Editor from '@monaco-editor/react';
import { NavMenu } from './NavMenu';
import React from 'react';
import { StackViewerFilter } from './StackViewerFilter'
import base64url from 'base64url';

export interface Props {
    match: any;
}

interface State {
    loading: boolean;
    sourceInformation: SourceInformation | null;
}

interface LineInformation {
    lineNumber: number;
    metric: number;
}

interface SourceInformation {
    url: string;
    log: string;
    summary: LineInformation[];
    data: string;
    buildTimeFilePath: string;
}

export class SourceViewer extends React.PureComponent<Props, State> {

    static displayName = SourceViewer.name;

    constructor(props: Props) {
        super(props);
        this.handleEditorDidMount = this.handleEditorDidMount.bind(this);
        this.state = { loading: true, sourceInformation: null };

        fetch('/api/getsource?' + StackViewerFilter.constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey) + '&name=' + this.props.match.params.callTreeNodeId, { method: 'GET', headers: { 'Content-Type': 'application/json' } })
            .then(res => res.json())
            .then(data => {
                this.setState({ sourceInformation: data, loading: false });
            });
    }

    handleEditorDidMount(_: any, editor: any) {

        let summary = this.state.sourceInformation?.summary;
        if (summary !== undefined && summary.length > 0) {

            editor.revealPosition({ lineNumber: summary[0].lineNumber, column: 1 }, true, false);

            summary.forEach(e => {
                editor.changeDecorations(function (changeAccessor: any) {
                    return changeAccessor.addDecoration({
                        startLineNumber: e.lineNumber,
                        startColumn: 1,
                        endLineNumber: e.lineNumber,
                        endColumn: 1
                    }, {
                        isWholeLine: true,
                        glyphMarginClassName: 'lineDecoration',
                        inlineClassName: 'inlineDecoration'
                    });
                });
            });
        }
    }

    static renderEditor(routeKey: string, sourceInformation: SourceInformation, obj: SourceViewer) {
        return (
            <div>
                <NavMenu dataFile={JSON.parse(base64url.decode(routeKey, "utf8")).a} />
                <div style={{ margin: 2 + 'px' }}>
                    <div style={{ margin: 10 + 'px' }}>
                        <h5><a href={sourceInformation.url}>{sourceInformation.buildTimeFilePath}</a></h5>
                    </div>
                    <table className="hotlines">
                        <thead>
                            <tr><td>Line Number</td><td>Hit Count</td></tr>
                        </thead>
                        <tbody>
                            {sourceInformation.summary.map(line =>
                                <tr key={`${line.lineNumber}`}>
                                    <td>{line.lineNumber}</td>
                                    <td>{line.metric}</td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                    <Editor editorDidMount={obj.handleEditorDidMount} theme="dark" height="90vh" language="csharp" options={{ readOnly: true, minimap: { enabled: false }, glyphMargin: true, scrollBeyondLastLine: false }} value={sourceInformation.data} line={sourceInformation.summary.length > 0 ? sourceInformation.summary[0].lineNumber : 1} />
                </div>
            </div>
        );
    }

    render() {
        return (this.state.loading || this.state.sourceInformation === null ? <p><em>Loading...</em></p> : SourceViewer.renderEditor(this.props.match.params.routeKey, this.state.sourceInformation, this));
    }
}