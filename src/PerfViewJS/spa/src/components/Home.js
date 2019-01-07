import React, { Component } from 'react';
import { Redirect } from 'react-router';
import base64url from 'base64url'

export class Home extends Component {
  static displayName = Home.name;

  constructor(props) {
    super(props);
    this.state = { dataFile: null, redirect: false };
    this.handleChange = this.handleChange.bind(this);
    this.handleOnClick = this.handleOnClick.bind(this);
  }

  handleChange(e) {
    this.setState({ dataFile: e.target.value });
  }

  handleOnClick() {
    this.setState({ redirect: true });
  }

  render() {

    if (this.state.redirect) {

        var encoded = base64url.encode(this.state.dataFile, "utf8");
      return <Redirect push to={`/ui/eventlist/${(encoded)}`} />;
    }

    return <div><input type="dataFile" onChange={this.handleChange} /><button className="btn btn-secondary btn-sm" onClick={this.handleOnClick}>Analyze</button></div>;
  }
}
