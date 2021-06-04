import base64url from "base64url";
import React from "react";

// eslint-disable-next-line @typescript-eslint/ban-ts-comment
//@ts-ignore
const DataFileContext = React.createContext<DataFileContextType>();
export type DataFileContextType = {
  dataFile: string;
  setDataFile: (dataFile: string) => void;
  dataFileName: string;
};

interface IDataFileContextProviderProp {
  children: JSX.Element;
}

export const DataFileContextProvider = (props: IDataFileContextProviderProp) => {
  const { children } = props;
  const [dataFile, _setDataFile] = React.useState("");
  const dataFileName = base64url.decode(dataFile, "utf8").replaceAll("*", "");

  const setDataFile = (dataFile: string) => {
    return _setDataFile(base64url.encode(dataFile + "*" + "" + "*" + "", "utf8"));
  };
  const value = { dataFile, setDataFile, dataFileName };
  return <DataFileContext.Provider value={value}>{children}</DataFileContext.Provider>;
};

export const useDataFileContext = () => {
  const context = React.useContext(DataFileContext);
  if (context === undefined) throw new Error("useDataFile context must be used within DataFileContextProvider");
  return context;
};
