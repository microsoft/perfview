import { IColumn } from "@fluentui/react";

export const HotspotsColDef: IColumn[] = [
  {
    key: "Name",
    name: "Name",
    isResizable: true,
    fieldName: "name",
    minWidth: 1000,
  },
  {
    key: "exclusiveMetricPercent",
    name: "Exclusive Metric %",
    fieldName: "exclusiveMetricPercent",
    minWidth: 80,
  },
  {
    key: "exclusiveCount",
    name: "Exclusive Count",
    fieldName: "exclusiveCount",
    minWidth: 80,
  },
  {
    key: "inclusiveMetricPercent",
    name: "Inclusive Metric %",
    fieldName: "inclusiveMetricPercent",
    minWidth: 55,
  },
  {
    key: "inclusiveCount",
    name: "Inclusive Count",
    fieldName: "inclusiveCount",
    minWidth: 80,
  },
  {
    key: "exclusiveFoldedMetric",
    name: "Fold Count",
    fieldName: "exclusiveFoldedMetric",
    minWidth: 50,
  },
  {
    key: "inclusiveMetricByTimeString",
    name: "When",
    isResizable: true,
    fieldName: "inclusiveMetricByTimeString",
    minWidth: 250,
  },
  {
    key: "firstTimeRelativeMSec",
    name: "First",
    fieldName: "firstTimeRelativeMSec",
    minWidth: 100,
  },
  {
    key: "lastTimeRelativeMSec",
    name: "Last",
    fieldName: "lastTimeRelativeMSec",
    minWidth: 100,
  },
];
