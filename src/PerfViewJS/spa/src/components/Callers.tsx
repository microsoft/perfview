import { EmptyTNode, TNode } from "./TNode";
import { RouteComponentProps } from "react-router";
import { Link } from "react-router-dom";
import React from "react";
import { TreeNode } from "./TreeNode";
import base64url from "base64url";
import { constructAPICacheKeyFromRouteKey } from "common/Utility";
import StackViewerFilter from "features/StackViewerFilter/StackViewerFilter";

interface MatchParams {
  routeKey: string;
  callTreeNodeId: string;
  //subtree?: string;
}

export interface Props extends RouteComponentProps<MatchParams> {}

interface State {
  loading: boolean;
  node: TNode;
}

export class Callers extends React.Component<Props, State> {
  ignoreLastFetch: boolean;

  static displayName = Callers.name;

  fetchData() {
    this.setState({ node: new EmptyTNode(), loading: true }); // HACK: Why is this required?

    fetch(
      "/api/treenode?" +
        constructAPICacheKeyFromRouteKey(this.props.match.params.routeKey) +
        "&name=" +
        this.props.match.params.callTreeNodeId +
        "&subtree=" +
        //this.props.match.params.subtree,
        { method: "GET", headers: { "Content-Type": "application/json" } }
    )
      .then((res) => res.json())
      .then((data) => {
        if (!this.ignoreLastFetch) {
          if (data === null) {
            window.location.href = "/ui/stackviewer/hotspotsOld/" + this.props.match.params.routeKey;
          } else {
            data.hasChildren = true; // HACK: Because the api doesn't return this set true.
            this.setState({ node: data, loading: false });
          }
        }
      });
  }

  constructor(props: Props) {
    super(props);
    this.ignoreLastFetch = false;
    this.state = { loading: true, node: new EmptyTNode() };
  }

  componentWillUnmount() {
    this.ignoreLastFetch = true;
  }

  componentDidMount() {
    this.fetchData();
  }

  componentDidUpdate(prevProps: Props) {
    const oldId = prevProps.match.params.callTreeNodeId;
    const newId = this.props.match.params.callTreeNodeId;
    if (newId !== oldId) {
      this.fetchData();
    }
  }

  static renderCallersTable(routeKey: string, callTreeNodeId: string, node: TNode) {
    return (
      <table className="table table-striped table-bordered" id="pd">
        <thead>
          <tr>
            <td className="center">Name</td>
            <td className="right">Source</td>
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
          <TreeNode
            routeKey={routeKey}
            callTreeNodeId={callTreeNodeId}
            node={node}
            indent={0}
            autoExpand={node.hasChildren}
          />
        </tbody>
      </table>
    );
  }

  render() {
    const contents = this.state.loading ? (
      <p>
        <em>Loading...</em>
      </p>
    ) : (
      Callers.renderCallersTable(
        this.props.match.params.routeKey,
        this.props.match.params.callTreeNodeId,
        this.state.node
      )
    );

    return (
      <div>
        <div style={{ margin: 2 + "px" }}>
          <div style={{ margin: 10 + "px" }}>
            <h4>
              {base64url.decode(JSON.parse(base64url.decode(this.props.match.params.routeKey)).l)} &raquo;{" "}
              <Link to={`/ui/stackviewer/hotspotsOld/${this.props.match.params.routeKey}`}>Hotspots</Link> &raquo;
              Callers
            </h4>
            <StackViewerFilter />
          </div>
          {contents}
        </div>
      </div>
    );
  }
}
