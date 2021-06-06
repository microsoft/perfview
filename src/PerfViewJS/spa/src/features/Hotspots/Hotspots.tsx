import { Link, RouteComponentProps } from "react-router-dom";
import React, { useEffect, useState } from "react";
import { StackViewerFilter } from "../../components/StackViewerFilter";
import { TNode } from "../../components/TNode";
import base64url from "base64url";
import { PrimaryButton } from "@fluentui/react";

interface IHotspotsProp {
  routeKey: string;
}
interface State {
  loading: boolean;
  nodes: TNode[];
}

const Hotspots: React.FC<IHotspotsProp> = (props: IHotspotsProp) => {
  const [loading, setLoading] = useState(false);
  const [nodes, setNodes] = useState<TNode[]>([]);
  const { routeKey } = props;

  useEffect(() => {
    fetch("/api/hotspots?" + StackViewerFilter.constructAPICacheKeyFromRouteKey(routeKey))
      .then((res) => res.json())
      .then((data) => {
        setNodes(data);
      });
  }, [routeKey]);

  const handleDrillIntoClick = (d: string, t: string) => {
    let drillType = "/api/drillinto/exclusive?";
    if (d === "i") {
      drillType = "/api/drillinto/inclusive?";
    }

    fetch(drillType + StackViewerFilter.constructAPICacheKeyFromRouteKey(routeKey) + "&name=" + t, {
      method: "GET",
      headers: { "Content-Type": "application/json" },
    })
      .then((res) => res.json())
      .then((data) => {
        const newRouteKey = JSON.parse(base64url.decode(routeKey, "utf8"));
        newRouteKey.k = data;
        window.location.href = "/ui/stackviewer/hotspots/" + base64url.encode(JSON.stringify(newRouteKey));
      });
  };

  const renderHotspotsTable = (nodes: TNode[], routeKey: string) => {
    return (
      <table className="table table-striped table-bordered" id="pd">
        <thead>
          <tr>
            <td className="center">Name</td>
            <td className="center">Exclusive Metric %</td>
            <td className="center">Exclusive Count</td>
            <td className="center">Inclusive Metric %</td>
            <td className="center">Inclusive Count</td>
            <td className="center">Fold Count</td>
            <td className="center">When</td>
            <td className="center">First</td>
            <td className="center">Last</td>
          </tr>
        </thead>
        <tbody>
          {nodes.map((node) => (
            <tr key={`${node.base64EncodedId}`}>
              <td>
                <Link to={`/ui/stackviewer/callers/${routeKey}/${node.base64EncodedId}`}>{node.name}</Link>
              </td>
              <td className="center">{node.exclusiveMetricPercent}%</td>
              <td className="center">
                {/* <PrimaryButton onClick={() => obj.handleDrillIntoClick("e", node.base64EncodedId)}>{node.exclusiveCount}</PrimaryButton> */}
              </td>
              <td className="center">{node.inclusiveMetricPercent}%</td>
              <td className="center">
                {/* <PrimaryButton onClick={() => obj.handleDrillIntoClick("i", node.base64EncodedId)}>{node.inclusiveCount}</PrimaryButton> */}
              </td>
              <td className="center">{node.exclusiveFoldedMetric}</td>
              <td className="center">{node.inclusiveMetricByTimeString}</td>
              <td className="center">{node.firstTimeRelativeMSec}</td>
              <td className="center">{node.lastTimeRelativeMSec}</td>
            </tr>
          ))}
        </tbody>
      </table>
    );
  };

  return (
    <div>
      <div style={{ margin: 2 + "px" }}>
        <div style={{ margin: 10 + "px" }}>
          {/* <h4>{base64url.decode(JSON.parse(base64url.decode(routeKey, "utf8")).l, "utf8")} &raquo; Hotspots</h4> */}
          <StackViewerFilter routeKey={routeKey}></StackViewerFilter>
        </div>
        {renderHotspotsTable(nodes, routeKey)}
      </div>
    </div>
  );
};
export default Hotspots;
