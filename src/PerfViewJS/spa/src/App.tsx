import React, { useState } from "react";

import { Callers } from "./components/Callers";
import { EventList } from "./components/EventList";
import { EventViewer } from "./components/EventViewer";
import Home from "./components/Home";
import { Hotspots } from "./components/Hotspots";
import Layout from "./components/Layout";
import { ModuleList } from "./components/ModuleList";
import { ProcessChooser } from "./components/ProcessChooser";
import { ProcessInfo } from "./components/ProcessInfo";
import { ProcessList } from "./components/ProcessList";
import { Route } from "react-router";
import { SourceViewer } from "./components/SourceViewer";
import { TraceInfo } from "./components/TraceInfo";
import { ThemeProvider, Toggle } from '@fluentui/react';
import {
  AzureThemeDark,
  AzureThemeLight
} from '@fluentui/azure-themes';
import { Col, Row } from "react-grid-system";
import { BrowserRouter, Switch } from "react-router-dom";
import './i18n/i18n';
import { useTranslation } from 'react-i18next';

const darkTheme = AzureThemeDark;
const lightTheme = AzureThemeLight;

const App: React.FC = () => {
  const [useDarkMode, setUseDarkMode] = useState(false);
  const { t } = useTranslation();
  return (
    <ThemeProvider applyTo="body" theme={useDarkMode ? darkTheme : lightTheme}>
      <BrowserRouter>
        <Switch>
          <Layout>
            <Row justify='end' align="center">
              <Col xs={10}><h2>{t('brand')}</h2></Col>
              <Col xs={2}>
                <Toggle onText="dark" offText="light" onChange={() => setUseDarkMode(!useDarkMode)} />
              </Col>
            </Row>
            <Route exact path="/" component={Home} />
            <Route exact path="/ui" component={Home} />
            <Route
              path="/ui/processInfo/:dataFile/:processIndex"
              component={ProcessInfo}
            />
            <Route path="/ui/traceInfo/:dataFile" component={TraceInfo} />
            <Route path="/ui/processList/:dataFile" component={ProcessList} />
            <Route path="/ui/moduleList/:dataFile" component={ModuleList} />
            <Route path="/ui/eventviewer/:dataFile" component={EventViewer} />
            <Route
              path="/ui/stackviewer/eventlist/:dataFile"
              component={EventList}
            />
            <Route
              path="/ui/stackviewer/processchooser/:dataFile/:stackType/:stackTypeName"
              component={ProcessChooser}
            />
            <Route path="/ui/stackviewer/hotspots/:routeKey" component={Hotspots} />
            <Route
              path="/ui/stackviewer/callers/:routeKey/:callTreeNodeId"
              component={Callers}
            />
            <Route
              path="/ui/sourceviewer/:routeKey/:callTreeNodeId"
              component={SourceViewer}
            />
          </Layout>
        </Switch>
      </BrowserRouter>
    </ThemeProvider>
  );
}

export default App;