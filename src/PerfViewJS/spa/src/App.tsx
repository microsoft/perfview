import "./i18n/i18n";

import {
  AzureThemeHighContrastDark,
  AzureThemeHighContrastLight,
  AzureThemeLight,
  AzureThemeDark,
} from "@fluentui/azure-themes";
import { CommandBar, ICommandBarItemProps, Theme, ThemeProvider } from "@fluentui/react";
import React, { useState } from "react";
import { Route } from "react-router";
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
import { SourceViewer } from "./components/SourceViewer";
import TraceInfo from "./components/TraceInfo";
import { DataFileContextProvider } from "./context/DataFileContext";

const App: React.FC = () => {
  const [theme, setTheme] = useState<Theme>(AzureThemeLight);
  const SettingsCommandBar: React.FunctionComponent = () => {
    return <CommandBar items={[]} farItems={themes} />;
  };

  const themes: ICommandBarItemProps[] = [
    {
      key: "newItem",
      text: "Theme",
      iconProps: { iconName: "Design" },
      subMenuProps: {
        items: [
          {
            key: "dark",
            text: "Dark",
            iconProps: { iconName: "CircleFill" },
            onClick: () => setTheme(AzureThemeDark),
          },
          {
            key: "light",
            text: "Light",
            iconProps: { iconName: "CircleRing" },
            onClick: () => setTheme(AzureThemeLight),
          },
          {
            key: "darkContrast",
            text: "Dark high contrast",
            iconProps: { iconName: "CircleStopSolid" },
            onClick: () => setTheme(AzureThemeHighContrastDark),
          },
          {
            key: "lightContrast",
            text: "Light high constrast",
            iconProps: { iconName: "CircleStop" },
            onClick: () => setTheme(AzureThemeHighContrastLight),
          },
        ],
      },
    },
  ];

  return (
    <ThemeProvider applyTo="body" theme={theme}>
      <DataFileContextProvider>
        <>
          <SettingsCommandBar />
          <Layout>
            <Route exact path="/index.html" component={Home} />
            <Route exact path="/" component={Home} />
            <Route exact path="/ui" component={Home} />
            <Route path="/ui/processInfo/:dataFile/:processIndex" component={ProcessInfo} />
            <Route path="/ui/traceInfo/:dataFile" component={TraceInfo} />
            <Route path="/ui/processList/:dataFile" component={ProcessList} />
            <Route path="/ui/moduleList/:dataFile" component={ModuleList} />
            <Route path="/ui/eventviewer/:dataFile" component={EventViewer} />
            <Route path="/ui/stackviewer/eventlist/:dataFile" component={EventList} />
            <Route
              path="/ui/stackviewer/processchooser/:dataFile/:stackType/:stackTypeName"
              component={ProcessChooser}
            />
            <Route path="/ui/stackviewer/hotspots/:routeKey" component={Hotspots} />
            <Route path="/ui/stackviewer/callers/:routeKey/:callTreeNodeId" component={Callers} />
            <Route path="/ui/sourceviewer/:routeKey/:callTreeNodeId" component={SourceViewer} />
          </Layout>
        </>
      </DataFileContextProvider>
    </ThemeProvider>
  );
};

export default App;
