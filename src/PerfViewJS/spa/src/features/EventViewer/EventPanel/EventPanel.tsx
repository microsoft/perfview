import {
  CheckboxVisibility,
  ConstrainMode,
  DetailsList,
  DetailsListLayoutMode,
  FontWeights,
  getTheme,
  IButtonStyles,
  IColumn,
  IconButton,
  IDetailsColumnRenderTooltipProps,
  IDetailsHeaderProps,
  IIconProps,
  IRenderFunction,
  mergeStyleSets,
  Modal,
  Panel,
  PanelType,
  PrimaryButton,
  ScrollablePane,
  ScrollbarVisibility,
  Sticky,
  StickyPositionType,
  TooltipHost,
} from "@fluentui/react";
import { useBoolean } from "@fluentui/react-hooks";
import React, { useEffect, useState } from "react";
import { useDataFileContext } from "context/DataFileContext";
import base64url from "base64url";
import Hotspots from "features/Hotspots";

interface IEventData {
  eventIndex: number;
  eventName: string;
  hasStack: boolean;
  processName: string;
  rest: string;
  timestamp: string;
}

interface IEventPanelProp {
  isOpen: boolean;
  dismissPanel: () => void;
  maxEventCount: number;
  start: string;
  end: string;
  textFilter: string;
  selectedEvents: string; //<-- csv?
}

const columns: IColumn[] = [
  {
    key: "eventName",
    name: "Event Name",
    fieldName: "eventName",
    minWidth: 510,
  },
  {
    key: "timestamp",
    name: "Time MSec",
    fieldName: "timestamp",
    minWidth: 80,
  },
  {
    key: "processName",
    name: "Process Name",
    fieldName: "processName",
    minWidth: 100,
  },
  {
    key: "hasStack",
    name: "Has Stack",
    fieldName: "hasStack",
    minWidth: 80,
  },
  {
    key: "rest",
    name: "Rest",
    fieldName: "rest",
    minWidth: 1200,
  },
];

const wrapperStyle: React.CSSProperties = { height: "100vh", position: "relative" };

const cancelIcon: IIconProps = { iconName: "Cancel" };

const theme = getTheme();
const contentStyles = mergeStyleSets({
  container: {
    display: "flex",
    flexFlow: "column nowrap",
    alignItems: "stretch",
  },
  header: [
    {
      flex: "1 1 auto",
      borderTop: `4px solid ${theme.palette.themePrimary}`,
      display: "flex",
      alignItems: "center",
      fontWeight: FontWeights.semibold,
      padding: "12px 12px 14px 24px",
    },
  ],
  body: {
    flex: "4 4 auto",
    padding: "0 24px 24px 24px",
    overflowY: "hidden",
  },
});
const iconButtonStyles: Partial<IButtonStyles> = {
  root: {
    marginLeft: "auto",
    marginTop: "4px",
    marginRight: "2px",
  },
};

const EventPanel: React.FC<IEventPanelProp> = (props) => {
  const { isOpen, maxEventCount, start, end, textFilter, selectedEvents, dismissPanel } = props;
  if (!isOpen) return <></>; //short circuit this component
  const { dataFile } = useDataFileContext();
  const [eventData, setEventData] = useState<IEventData[]>([]);
  const [isModalOpen, { setTrue: showModal, setFalse: hideModal }] = useBoolean(false);
  const [routeKey, setRouteKey] = useState<string>("");

  useEffect(() => {
    fetch(
      `/api/eventdata?filename=${dataFile}&maxEventCount=${maxEventCount}&start=${start}&end=${end}&filter=${base64url.encode(
        textFilter,
        "utf8"
      )}&eventTypes=${selectedEvents}`
    )
      .then((res) => res.json())
      .then((data) => {
        setEventData(data);
      });
  }, [dataFile, maxEventCount, start, end, textFilter, selectedEvents, dismissPanel]);

  const fetchHotSpots = (eventIndex: number) => {
    const encodedRoute = `${base64url.encode(
      JSON.stringify({
        a: dataFile,
        b: "-1",
        c: "-1",
        d: (parseFloat(eventData[eventIndex].timestamp) - 0.001).toFixed(3),
        e: (parseFloat(eventData[eventIndex].timestamp) + 0.001).toFixed(3),
        f: "",
        g: "",
        h: "",
        i: "",
        j: "",
        k: "",
        l: base64url.encode("Any Event", "utf8"),
      }),
      "utf8"
    )}`;
    setRouteKey(encodedRoute);
    console.log(encodedRoute);
    showModal();
  };

  const renderFixedDetailsHeader: IRenderFunction<IDetailsHeaderProps> = (props, defaultRender) => {
    if (!props) {
      return null;
    }
    const onRenderColumnHeaderTooltip: IRenderFunction<IDetailsColumnRenderTooltipProps> = (tooltipHostProps) => (
      <TooltipHost {...tooltipHostProps} />
    );
    return (
      <Sticky stickyPosition={StickyPositionType.Header} isScrollSynced>
        {defaultRender?.({ ...props, onRenderColumnHeaderTooltip })}
      </Sticky>
    );
  };

  const renderItemColumn = (item?: IEventData, index?: number, column?: IColumn) => {
    if (column?.fieldName === "hasStack" && item?.hasStack) {
      return <PrimaryButton onClick={() => fetchHotSpots(item.eventIndex)}>View Stack</PrimaryButton>;
    } else {
      //? everything is optional..
      // eslint-disable-next-line @typescript-eslint/ban-ts-comment
      //@ts-ignore
      return item[column.fieldName];
    }
  };

  return (
    <Panel
      isOpen={isOpen}
      type={PanelType.smallFluid}
      closeButtonAriaLabel="Close"
      isHiddenOnDismiss={true}
      headerText="Events"
      onDismiss={dismissPanel}
    >
      <div style={wrapperStyle}>
        <ScrollablePane scrollbarVisibility={ScrollbarVisibility.auto}>
          <DetailsList
            setKey={"key"}
            checkboxVisibility={CheckboxVisibility.hidden}
            layoutMode={DetailsListLayoutMode.justified}
            constrainMode={ConstrainMode.unconstrained}
            compact={true}
            items={eventData}
            columns={columns}
            onRenderDetailsHeader={renderFixedDetailsHeader}
            onRenderItemColumn={renderItemColumn}
          ></DetailsList>
        </ScrollablePane>
      </div>
      <Modal isOpen={isModalOpen} onDismiss={hideModal} isBlocking={false}>
        <div className={contentStyles.header}>
          <span>Stack</span>
          <IconButton
            styles={iconButtonStyles}
            iconProps={cancelIcon}
            ariaLabel="Close popup modal"
            onClick={hideModal}
          />
        </div>
        <div className={contentStyles.body}>
          <Hotspots routeKey={routeKey} />
        </div>
      </Modal>
    </Panel>
  );
};

export default EventPanel;
