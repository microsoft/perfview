import React, { Component } from 'react';

import { Callers } from './components/Callers';
import { EventList } from './components/EventList';
import { EventViewer } from './components/EventViewer';
import { Home } from './components/Home';
import { Hotspots } from './components/Hotspots';
import { Layout } from './components/Layout';
import { ModuleList } from './components/ModuleList';
import { ProcessChooser } from './components/ProcessChooser';
import { ProcessInfo } from './components/ProcessInfo';
import { ProcessList } from './components/ProcessList';
import { Route } from 'react-router';
import { SourceViewer } from './components/SourceViewer';
import { TraceInfo } from './components/TraceInfo';

export default class App extends Component {
    static displayName = App.name;

    render() {
        return (
            <Layout>
                <Route exact path='/' component={Home} />
                <Route exact path='/ui' component={Home} />
                <Route path='/ui/processInfo/:dataFile/:processIndex' component={ProcessInfo} />
                <Route path='/ui/traceInfo/:dataFile' component={TraceInfo} />
                <Route path='/ui/processList/:dataFile' component={ProcessList} />
                <Route path='/ui/moduleList/:dataFile' component={ModuleList} />
                <Route path='/ui/eventviewer/:dataFile' component={EventViewer} />
                <Route path='/ui/stackviewer/eventlist/:dataFile' component={EventList} />
                <Route path='/ui/stackviewer/processchooser/:dataFile/:stackType/:stackTypeName' component={ProcessChooser} />
                <Route path='/ui/stackviewer/hotspots/:routeKey' component={Hotspots} />
                <Route path='/ui/stackviewer/callers/:routeKey/:callTreeNodeId' component={Callers} />
                <Route path='/ui/sourceviewer/:routeKey/:callTreeNodeId' component={SourceViewer} />
            </Layout>
        );
    }
}
