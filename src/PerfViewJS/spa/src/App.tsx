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
import { Callers } from "./features/Callers/Callers";
import { EventList } from "./features/EventList.tsx/EventList";
import { Layout } from "./components/Layout";
import { ModuleList } from "./components/ModuleList";
import { ProcessChooser } from "./features/ProcessChooser/ProcessChooser";
import { ProcessInfo } from "./components/ProcessInfo";
import { ProcessList } from "./features/ProcessList/ProcessList";
import { SourceViewer } from "./components/SourceViewer";
import { DataFileContextProvider } from "./context/DataFileContext";
import { EventViewer } from "./features/EventViewer";
import { Hotspots } from "features/Hotspots/Hotspots";
import { Toaster } from "react-hot-toast";
import { Routes } from "common/Routes";
import { RouteKeyContextProvider } from "context/RouteContext";
import { Home } from "features/Home/Home";
import { TraceInfo } from "features/TraceInfo/TraceInfo";

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
      <SettingsCommandBar />
      <DataFileContextProvider>
        <RouteKeyContextProvider>
          <Layout>
            <Route exact path={Routes.IndexHtml} component={Home} />
            <Route exact path={Routes.Root} component={Home} />
            <Route exact path={Routes.UI} component={Home} />
            <Route path={Routes.ProcessInfo} component={ProcessInfo} />
            <Route path={Routes.TraceInfo} component={TraceInfo} />
            <Route path={Routes.ProcessList} component={ProcessList} />
            <Route path={Routes.ModuleList} component={ModuleList} />
            <Route path={Routes.EventViewer} component={EventViewer} />
            <Route path={Routes.EventList} component={EventList} />
            <Route path={Routes.ProcessChooser} component={ProcessChooser} />
            <Route path={Routes.HotSpots} component={Hotspots} />
            <Route path={Routes.Callers} component={Callers} />
            <Route path={Routes.SourceViewer} component={SourceViewer} />
          </Layout>
        </RouteKeyContextProvider>
      </DataFileContextProvider>
      <Toaster
        position="top-right"
        containerStyle={{
          zIndex: 1000001,
        }}
        toastOptions={{
          error: {
            duration: Infinity,
            style: {
              backgroundColor: theme.semanticColors.severeWarningBackground,
              color: theme.semanticColors.bodyText,
            },
          },
          success: {
            duration: 5000,
            style: {
              backgroundColor: theme.semanticColors.successBackground,
              color: theme.semanticColors.bodyText,
            },
          },
        }}
      />
    </ThemeProvider>
  );
};

export { App };
