import React, { useEffect, useState } from "react";
import {
  Nav,
  INavStyles,
  INavLinkGroup,
  INavLink,
} from "@fluentui/react/lib/Nav";
import { ScrollablePane } from '@fluentui/react';
import { RouteComponentProps, withRouter, useHistory } from "react-router-dom";
interface MatchParams {
  dataFile?: string;
}
export interface INavMenuProps extends RouteComponentProps<MatchParams> { }

const navStyles: Partial<INavStyles> = {
  root: {
    height: '100%',
    maxWidth: 200
  },
};

const navLinkGroups: INavLinkGroup[] = [
  {
    name: 'PerfViewJS ',
    links: []
  }
];

const NavMenu: React.FC = () => {
  const history = useHistory<MatchParams>();
  const [menuState, setMenuState] = useState<INavLinkGroup[]>(navLinkGroups);
  const dataFile = (history.location.state && history.location.state.dataFile) || '';
  useEffect(() => {
    const onLinkClick = (ev?: React.MouseEvent<HTMLElement, MouseEvent> | undefined, item?: INavLink | undefined) => {
      ev?.preventDefault();
      if (!item) return;
      if (item.url === '/')
        history.push(item.url);
      else {
        history.push(item.url + dataFile, { dataFile });
      }
    }
    const navLinks: INavLink[] = [
      {
        name: "Load file",
        url: "/",
        onClick: onLinkClick
      }];
    //workaround for dynamically setting fluent-ui navLinks
    if (history.location.state && history.location.state.dataFile !== "") {
      navLinks.push(
        {
          name: "Trace Info",
          url: "/ui/traceinfo/" + dataFile,
          onClick: onLinkClick,
          key: '/ui/traceinfo/'
        },
        {
          name: "Event Viewer",
          url: "/ui/eventviewer/" + dataFile,
          onClick: onLinkClick,
          key: '/ui/eventviewer/'
        },
        {
          name: "Stack Viewer",
          url: "/ui/stackviewer/eventlist/" + dataFile,
          onClick: onLinkClick,
          key: '/ui/stackviewer/eventlist/'
        },
        {
          name: "Process List",
          url: "/ui/processlist/" + dataFile,
          onClick: onLinkClick,
          key: "/ui/processlist/"
        },
        {
          name: "Module List",
          url: "/ui/modulelist/" + dataFile,
          onClick: onLinkClick,
          key: "/ui/modulelist/"
        })
    }
    menuState[0].links = navLinks;
    setMenuState([...menuState]);
  }, [history.location.state])

  return (
    <ScrollablePane scrollbarVisibility={"auto"}>
      <Nav styles={navStyles} groups={navLinkGroups} />
    </ScrollablePane>
  );
}

export default withRouter(NavMenu);
