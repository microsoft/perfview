import './NavMenu.css';

import { Collapse, Container, NavItem, NavLink, Navbar, NavbarBrand, NavbarToggler } from 'reactstrap';

import { Link } from 'react-router-dom';
import React from 'react';
import base64url from 'base64url'

export interface Props {
    dataFile: string;
}

interface State {
    collapsed: boolean;
    dataFile: string;
    fileName: string;
    startTime: string;
    endTime: string;
}

export class NavMenu extends React.Component<Props, State> {

    static displayName = NavMenu.name;

    constructor(props: Props) {
        super(props);
        let arr = base64url.decode(this.props.dataFile, "utf8").split('*');
        this.toggleNavbar = this.toggleNavbar.bind(this);
        this.state = {
            fileName: arr[0],
            startTime: arr[1],
            endTime: arr[2],
            collapsed: true,
            dataFile: props.dataFile,
        };
    }

    toggleNavbar() {
        this.setState({
            collapsed: !this.state.collapsed
        });
    }

    render() {
        return (
            <header>
                <Navbar className="navbar-expand-sm navbar-toggleable-sm ng-white border-bottom box-shadow mb-3" light>
                    {this.state.fileName}&nbsp;&nbsp;&nbsp;&nbsp; {this.state.startTime !== '' || this.state.endTime !== '' ? <strong>Time Filter Applied</strong> : null}&nbsp;{this.state.startTime !== '' ? <span>Start: {this.state.startTime}</span> : null} &nbsp; {this.state.endTime !== '' ? <span> End: {this.state.endTime}</span> : null}
                    <Container>
                        <NavbarBrand>PerfViewJS</NavbarBrand>
                        <NavbarToggler onClick={this.toggleNavbar} className="mr-2" />
                        <Collapse className="d-sm-inline-flex flex-sm-row-reverse" isOpen={!this.state.collapsed} navbar>
                            <ul className="navbar-nav flex-grow">
                                <NavItem>
                                    <NavLink tag={Link} className="text-dark" to={`/ui/traceinfo/` + this.state.dataFile}>Trace Info</NavLink>
                                </NavItem>
                                <NavItem>
                                    <NavLink tag={Link} className="text-dark" to={`/ui/eventviewer/` + this.state.dataFile}>Event Viewer</NavLink>
                                </NavItem>
                                <NavItem>
                                    <NavLink tag={Link} className="text-dark" to={`/ui/stackviewer/eventlist/` + this.state.dataFile}>Stack Viewer</NavLink>
                                </NavItem>
                                <NavItem>
                                    <NavLink tag={Link} className="text-dark" to={`/ui/processlist/` + this.state.dataFile}>Process List</NavLink>
                                </NavItem>
                                <NavItem>
                                    <NavLink tag={Link} className="text-dark" to={`/ui/modulelist/` + this.state.dataFile}>Module List</NavLink>
                                </NavItem>
                            </ul>
                        </Collapse>
                    </Container>
                </Navbar>
            </header>
        );
    }
}
