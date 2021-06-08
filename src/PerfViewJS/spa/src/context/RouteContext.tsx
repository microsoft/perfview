import React from "react";
import useLocalStorage from "../hooks/useLocalStorage";

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore
const RouteKeyContext = React.createContext<RouteKeyContextType>();

export type RouteKeyContextType = {
  routeKey: string;
  setRouteKey: (routeKey: string) => void;
};

interface IRouteKeyContextProviderProp {
  children: JSX.Element;
}

export const RouteKeyContextProvider = (props: IRouteKeyContextProviderProp) => {
  const { children } = props;
  const [routeKey, _setRouteKey] = useLocalStorage<string>("routeKey", "");

  const setRouteKey = (_routeKey: string) => _setRouteKey(_routeKey);
  const value = { routeKey, setRouteKey };
  return <RouteKeyContext.Provider value={value}>{children}</RouteKeyContext.Provider>;
};

export const useRouteKeyContext = () => {
  const context = React.useContext(RouteKeyContext);
  if (context === undefined) throw new Error("useRouteKeyContext context must be used within RouteKeyContextProvider");
  return context;
};
