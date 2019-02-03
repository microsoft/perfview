# PerfViewJS

PerfViewJS is a webviewer for ETL and NetPerf data.

## Usage

* dotnet run
* Browse to http://localhost:5000
* Type in the location of your PerfViewData.etl (must be merged)

## Debugging

* Press F5 in Visual Studio (or VSCode)
* cd spa
* npm start
* Browse to http://localhost:3000

## Todo

* Wrap PerfViewJS as a dotnet global tool
* Use Chromium Embedded Framework to make a client-side application

## Components

PerfViewJS is an ASP.NET Core application. React is used for rendering and GUI state.