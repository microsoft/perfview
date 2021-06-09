import React, { useEffect, useState } from "react";
import { StackViewerFilter } from "../StackViewerFilter/StackViewerFilter";
import { TNode } from "../../components/TNode";
import base64url from "base64url";
import { CheckboxVisibility, DetailsList, IDetailsHeaderProps, IRenderFunction, PrimaryButton } from "@fluentui/react";

import { Container, Row } from "react-grid-system";
import { HotspotsColDef } from "./HotspotsColDef";
import { constructAPICacheKeyFromRouteKey } from "common/Utility";
import { useRouteKeyContext } from "context/RouteContext";

import { IColumn } from "@fluentui/react";
import { useHistory } from "react-router";
import { TextLink } from "components/TextLink/TextLink";

const Hotspots: React.FC = () => {
  const [nodes, setNodes] = useState<TNode[]>([]);
  const history = useHistory();
  const { routeKey, setRouteKey } = useRouteKeyContext();

  useEffect(() => {
    fetch("/api/hotspots?" + constructAPICacheKeyFromRouteKey(routeKey))
      .then((res) => res.json())
      .then((data) => {
        setNodes(data);
      });
  }, [routeKey]);

  const renderItemColumn = (item?: TNode, index?: number, column?: IColumn) => {
    if (column?.fieldName === "exclusiveCount") {
      return (
        <PrimaryButton onClick={() => handleDrillIntoClick("e", item?.base64EncodedId)}>
          {item?.exclusiveCount}
        </PrimaryButton>
      );
    } else if (column?.fieldName === "inclusiveCount") {
      return (
        <PrimaryButton onClick={() => handleDrillIntoClick("i", item?.base64EncodedId)}>
          {item?.inclusiveCount}
        </PrimaryButton>
      );
    } else if (column?.fieldName === "name") {
      return (
        <TextLink
          onClick={() => {
            history.push(`/ui/stackviewer/callers/${routeKey}/${item?.base64EncodedId}`);
          }}
          content={item?.name || ""}
        />
      );
    } else {
      //? everything is optional..
      // eslint-disable-next-line @typescript-eslint/ban-ts-comment
      //@ts-ignore
      return item[column.fieldName];
    }
  };

  const handleDrillIntoClick = (d: string, nodeBase64EncodedId?: string) => {
    let drillType = "/api/drillinto/exclusive?";
    if (d === "i") {
      drillType = "/api/drillinto/inclusive?";
    }

    fetch(drillType + constructAPICacheKeyFromRouteKey(routeKey) + "&name=" + nodeBase64EncodedId)
      .then((res) => res.json())
      .then((data) => {
        const newRouteKey = JSON.parse(base64url.decode(routeKey));
        newRouteKey.k = data;
        setRouteKey(base64url.encode(JSON.stringify(newRouteKey)));
      });
  };

  const onRenderStackHeader: IRenderFunction<IDetailsHeaderProps> = (headerProps, defaultRender) => {
    //https://github.com/microsoft/fluentui/issues/12148#issuecomment-593573808
    if (!headerProps) return null;
    if (!defaultRender) return null;
    return defaultRender({
      ...headerProps,
      styles: {
        root: {
          selectors: {
            ".ms-DetailsHeader-cell": {
              whiteSpace: "normal",
              textOverflow: "clip",
              lineHeight: "normal",
            },
          },
        },
      },
    });
  };

  return (
    <Container>
      <Row>
        <h4>{base64url.decode(JSON.parse(base64url.decode(routeKey)).l)} &raquo; Hotspots</h4>
      </Row>
      <StackViewerFilter />
      <Row>
        <DetailsList
          checkboxVisibility={CheckboxVisibility.hidden}
          setKey={"key"}
          compact={true}
          items={nodes}
          columns={HotspotsColDef}
          onRenderDetailsHeader={onRenderStackHeader}
          onRenderItemColumn={renderItemColumn}
        />
      </Row>
    </Container>
  );
};

export { Hotspots };
