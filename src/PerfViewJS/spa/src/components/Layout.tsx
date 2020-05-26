import './Layout.css';

import React, { Component } from 'react';

export class Layout extends Component {
    static displayName = Layout.name;

    render() {
        return (
            <div>
                {this.props.children}
            </div>
        );
    }
}