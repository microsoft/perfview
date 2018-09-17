using Microsoft.Diagnostics.Tracing.Parsers;
// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsTCPIP;

namespace Microsoft.Diagnostics.Tracing
{
    // TODO FIX NOW NOT DONE

    /// <summary>
    /// A TcpIpComputer keeps track of TCP/IP connections so that you can correlate individual reads and
    /// writes with the connection info (like the IP address of each end), as well as data packets being
    /// sent (if you have packet capture turned on).  
    /// </summary>
    public class TcpIpComputer
    {
        /// <summary>
        /// Create a new GCRefernece computer from the stream of events 'source'.   When 'source' is processed
        /// you can call 'GetReferenceForGCAddress' to get stable ids for GC references.  
        /// </summary>
        /// <param name="source"></param>
        public TcpIpComputer(TraceEventDispatcher source)
        {
            m_source = source;

            var tcpParser = new MicrosoftWindowsTCPIPTraceEventParser(m_source);
            tcpParser.TcpRequestConnect += delegate (TcpRequestConnectArgs data)
            {
            };

            tcpParser.TcpRequestConnect += delegate (TcpRequestConnectArgs data)
            {
            };

            tcpParser.TcpDeliveryIndicated += delegate (TcpDisconnectTcbInjectFailedArgs data)
            {
            };

            tcpParser.TcpDataTransferReceive += delegate (TcpDataTransferReceiveArgs data)
            {
            };

            tcpParser.TcpSendPosted += delegate (TcpSendPostedArgs data)
            {
            };

            tcpParser.TcpCloseTcbRequest += delegate (TcpAccpetListenerRouteLookupFailureArgs args)
            {
            };

        }

        private TraceEventDispatcher m_source;
    }
}
