import React, { Component } from 'react';
import { Route } from 'react-router';
import { Layout } from './components/Layout';
import { Home } from './components/Home';
import { EventList } from './components/EventList';
import { ProcessList } from './components/ProcessList';
import { Hotspots } from './components/Hotspots';
import { Callers } from './components/Callers';
import { EventViewer } from './components/EventViewer';

export default class App extends Component {
  static displayName = App.name;

  render() {
    return (
      <Layout>
        <Route exact path='/' component={Home} />
        <Route path='/ui/events/:dataFile' component={EventViewer} />
        <Route path='/ui/eventlist/:dataFile' component={EventList} />
        <Route path='/ui/processlist/:dataFile/:stackType' component={ProcessList} />
        <Route path='/ui/hotspots/:routeKey' component={Hotspots} />
        <Route path='/ui/callers/:routeKey/:callTreeNodeId' component={Callers} />
      </Layout>
    );
  }
}
