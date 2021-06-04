import { contextBridge, ipcRenderer } from "electron";
import { IToElectronBridgeChannel, IFromElectronBridgeChannel, IElectronBridgeAction } from "./global";

const validChannels = ["toMain", "fromMain"];

//https://github.com/electron/electron/issues/21437#issuecomment-802288574
contextBridge.exposeInMainWorld("api", {
  send: (channel: IToElectronBridgeChannel, filePath: string) => {
    if (validChannels.includes(channel)) {
      ipcRenderer.send(channel, filePath);
    }
  },
  receive: (channel: IFromElectronBridgeChannel, func: (action: IElectronBridgeAction) => void) => {
    if (validChannels.includes(channel)) {
      const subscription = (event: Event, action: IElectronBridgeAction) => func(action);
      ipcRenderer.on(channel, subscription);
      return () => {
        ipcRenderer.removeListener(channel, subscription);
      };
    }
  },
});
