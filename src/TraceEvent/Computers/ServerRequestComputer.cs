// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Etlx;
using System;

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// Calculates stacks grouping them by the server request (e.g. ASP.NET) request they are for)
    /// </summary>
    public class ServerRequestComputer
    {
        /// <summary>
        /// Create a new ServerRequest Computer.
        /// </summary>
        public ServerRequestComputer(TraceEventDispatcher source)
        {
        }

        /// <summary>
        /// The server request that we currently processing
        /// </summary>
        private ServerRequest GetCurrentRequest(TraceThread thread)
        {
            return null;
        }
    }

    /// <summary>
    /// A ServerRequest contains all the information we know about a server request (e.g. ASP.NET request)
    /// </summary>
    public class ServerRequest
    {
        /// <summary>
        /// Any URL associated with the request
        /// </summary>
        public string Url;
        /// <summary>
        /// If the request has a GUID associated with it to uniquely identify it, this is it
        /// </summary>
        public Guid ID;

        /// <summary>
        /// The time that the request started (or the earliest that we know about it)
        /// </summary>
        public DateTime StartTime;
    }
}