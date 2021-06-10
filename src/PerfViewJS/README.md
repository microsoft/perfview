# PerfViewJS

PerfViewJS is a webviewer for ETL and NetPerf data.

## Development on VS Code

## Browser

This will run backend in debug mode

### Backend

- cmd+shift+D or launch backend via debug toolbar
- Place trace files in `"${workspaceFolder}/spa/tmp"`

### Frontend

- cd spa
- `npm run start`
- Browse to http://localhost:5000

## **or**

#### Electron

This will run backend in electron shell

- `npm run dev`

## Todo

- [ ] Refresh UI from HTML to Fluent-UI components
- [x] Use Chromium Embedded Framework to make a client-side application
- [x] Sortable Hotspots
- [ ] Wrap PerfViewJS as a dotnet global tool
