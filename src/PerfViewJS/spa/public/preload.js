const { contextBridge, ipcRenderer } = require("electron");

//https://github.com/electron/electron/issues/21437#issuecomment-802288574
const listeners = {};

contextBridge.exposeInMainWorld("api", {
  send: (channel, filePath) => {
    // whitelist channels
    let validChannels = ["toMain"];
    if (validChannels.includes(channel)) {
      ipcRenderer.send(channel, filePath);
    }
  },
  receive: (channel, func) => {
    let validChannels = ["fromMain"];
    if (validChannels.includes(channel)) {
      const subscription = (event, ...args) => func(...args);
      ipcRenderer.on(channel, subscription);
      return () => {
        ipcRenderer.removeListener(channel, subscription);
      };
    }
  },
});
