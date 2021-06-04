import "./Layout.css";
import { Container, Row, Col } from 'react-grid-system';
import React from "react";
import NavMenu from "./NavMenu";

interface ILayoutProps {
  children: React.ReactNode;
}

const Layout = (props: ILayoutProps) => {
  const { children } = props;
  return (
    <Container fluid style={{ margin: 0 }}>
      <Row style={{ height: "100%", minHeight: "100vh" }}>
        <Col xs={3}>
          <NavMenu />
        </Col>
        <Col xs={9}>
          {children}
        </Col>
      </Row>
    </Container>)
}

export default Layout;