using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class TestParser : TraceEventParser
    {
        public TestParser(TraceEventSource source) : base(source) { }

        public static string ProviderName = "TestProvider";
        protected override string GetProviderName() { return ProviderName; }

        private static List<TraceEvent> s_templates;
        protected internal override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new List<TraceEvent>();
                templates.Add(new EmptyTraceData(null, Event001Id, 0, "Event001", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event002Id, 0, "Event002", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event003Id, 0, "Event003", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event004Id, 0, "Event004", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event005Id, 0, "Event005", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event006Id, 0, "Event006", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event007Id, 0, "Event007", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event008Id, 0, "Event008", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event009Id, 0, "Event009", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event010Id, 0, "Event010", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event011Id, 0, "Event011", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event012Id, 0, "Event012", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event013Id, 0, "Event013", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event014Id, 0, "Event014", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event015Id, 0, "Event015", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event016Id, 0, "Event016", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event017Id, 0, "Event017", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event018Id, 0, "Event018", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event019Id, 0, "Event019", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event020Id, 0, "Event020", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event021Id, 0, "Event021", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event022Id, 0, "Event022", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event023Id, 0, "Event023", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event024Id, 0, "Event024", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event025Id, 0, "Event025", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event026Id, 0, "Event026", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event027Id, 0, "Event027", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event028Id, 0, "Event028", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event029Id, 0, "Event029", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event030Id, 0, "Event030", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event031Id, 0, "Event031", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event032Id, 0, "Event032", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event033Id, 0, "Event033", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event034Id, 0, "Event034", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event035Id, 0, "Event035", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event036Id, 0, "Event036", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event037Id, 0, "Event037", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event038Id, 0, "Event038", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                templates.Add(new EmptyTraceData(null, Event039Id, 0, "Event039", Guid.Empty, 0, "", ProviderGuid, ProviderName));
                s_templates = templates;
            }
            foreach (var template in s_templates)
            {
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                {
                    callback(template);
                }
            }
        }

        public static Guid ProviderGuid = new Guid(unchecked((int)0x01020304), 0x0506, 0x0708, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16);

        #region events
        public const int Event001Id = 1;
        public event Action<EmptyTraceData> Event001
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event001Id, 0, "Event001", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event001Id, ProviderGuid);
            }
        }
        public const int Event002Id = 2;
        public event Action<EmptyTraceData> Event002
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event002Id, 0, "Event002", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event002Id, ProviderGuid);
            }
        }
        public const int Event003Id = 3;
        public event Action<EmptyTraceData> Event003
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event003Id, 0, "Event003", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event003Id, ProviderGuid);
            }
        }
        public const int Event004Id = 4;
        public event Action<EmptyTraceData> Event004
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event004Id, 0, "Event004", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event004Id, ProviderGuid);
            }
        }
        public const int Event005Id = 5;
        public event Action<EmptyTraceData> Event005
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event005Id, 0, "Event005", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event005Id, ProviderGuid);
            }
        }
        public const int Event006Id = 6;
        public event Action<EmptyTraceData> Event006
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event006Id, 0, "Event006", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event006Id, ProviderGuid);
            }
        }
        public const int Event007Id = 7;
        public event Action<EmptyTraceData> Event007
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event007Id, 0, "Event007", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event007Id, ProviderGuid);
            }
        }
        public const int Event008Id = 8;
        public event Action<EmptyTraceData> Event008
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event008Id, 0, "Event008", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event008Id, ProviderGuid);
            }
        }
        public const int Event009Id = 9;
        public event Action<EmptyTraceData> Event009
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event009Id, 0, "Event009", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event009Id, ProviderGuid);
            }
        }

        public const int Event010Id = 10;
        public event Action<EmptyTraceData> Event010
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event010Id, 0, "Event010", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event010Id, ProviderGuid);
            }
        }
        public const int Event011Id = 11;
        public event Action<EmptyTraceData> Event011
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event011Id, 0, "Event011", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event011Id, ProviderGuid);
            }
        }
        public const int Event012Id = 12;
        public event Action<EmptyTraceData> Event012
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event012Id, 0, "Event012", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event012Id, ProviderGuid);
            }
        }
        public const int Event013Id = 13;
        public event Action<EmptyTraceData> Event013
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event013Id, 0, "Event013", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event013Id, ProviderGuid);
            }
        }
        public const int Event014Id = 14;
        public event Action<EmptyTraceData> Event014
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event014Id, 0, "Event014", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event014Id, ProviderGuid);
            }
        }
        public const int Event015Id = 15;
        public event Action<EmptyTraceData> Event015
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event015Id, 0, "Event015", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event015Id, ProviderGuid);
            }
        }
        public const int Event016Id = 16;
        public event Action<EmptyTraceData> Event016
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event016Id, 0, "Event016", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event016Id, ProviderGuid);
            }
        }
        public const int Event017Id = 17;
        public event Action<EmptyTraceData> Event017
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event017Id, 0, "Event017", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event017Id, ProviderGuid);
            }
        }
        public const int Event018Id = 18;
        public event Action<EmptyTraceData> Event018
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event018Id, 0, "Event018", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event018Id, ProviderGuid);
            }
        }
        public const int Event019Id = 19;
        public event Action<EmptyTraceData> Event019
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event019Id, 0, "Event019", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event019Id, ProviderGuid);
            }
        }

        public const int Event020Id = 20;
        public event Action<EmptyTraceData> Event020
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event020Id, 0, "Event020", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event020Id, ProviderGuid);
            }
        }
        public const int Event021Id = 21;
        public event Action<EmptyTraceData> Event021
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event021Id, 0, "Event021", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event021Id, ProviderGuid);
            }
        }
        public const int Event022Id = 22;
        public event Action<EmptyTraceData> Event022
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event022Id, 0, "Event022", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event022Id, ProviderGuid);
            }
        }
        public const int Event023Id = 23;
        public event Action<EmptyTraceData> Event023
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event023Id, 0, "Event023", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event023Id, ProviderGuid);
            }
        }
        public const int Event024Id = 24;
        public event Action<EmptyTraceData> Event024
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event024Id, 0, "Event024", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event024Id, ProviderGuid);
            }
        }
        public const int Event025Id = 25;
        public event Action<EmptyTraceData> Event025
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event025Id, 0, "Event025", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event025Id, ProviderGuid);
            }
        }
        public const int Event026Id = 26;
        public event Action<EmptyTraceData> Event026
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event026Id, 0, "Event026", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event026Id, ProviderGuid);
            }
        }
        public const int Event027Id = 27;
        public event Action<EmptyTraceData> Event027
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event027Id, 0, "Event027", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event027Id, ProviderGuid);
            }
        }
        public const int Event028Id = 28;
        public event Action<EmptyTraceData> Event028
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event028Id, 0, "Event028", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event028Id, ProviderGuid);
            }
        }
        public const int Event029Id = 29;
        public event Action<EmptyTraceData> Event029
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event029Id, 0, "Event029", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event029Id, ProviderGuid);
            }
        }

        public const int Event030Id = 30;
        public event Action<EmptyTraceData> Event030
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event030Id, 0, "Event030", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event030Id, ProviderGuid);
            }
        }
        public const int Event031Id = 31;
        public event Action<EmptyTraceData> Event031
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event031Id, 0, "Event031", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event031Id, ProviderGuid);
            }
        }
        public const int Event032Id = 32;
        public event Action<EmptyTraceData> Event032
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event032Id, 0, "Event032", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event032Id, ProviderGuid);
            }
        }
        public const int Event033Id = 33;
        public event Action<EmptyTraceData> Event033
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event033Id, 0, "Event033", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event033Id, ProviderGuid);
            }
        }
        public const int Event034Id = 34;
        public event Action<EmptyTraceData> Event034
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event034Id, 0, "Event034", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event034Id, ProviderGuid);
            }
        }
        public const int Event035Id = 35;
        public event Action<EmptyTraceData> Event035
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event035Id, 0, "Event035", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event035Id, ProviderGuid);
            }
        }
        public const int Event036Id = 36;
        public event Action<EmptyTraceData> Event036
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event036Id, 0, "Event036", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event036Id, ProviderGuid);
            }
        }
        public const int Event037Id = 37;
        public event Action<EmptyTraceData> Event037
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event037Id, 0, "Event037", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event037Id, ProviderGuid);
            }
        }
        public const int Event038Id = 38;
        public event Action<EmptyTraceData> Event038
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event038Id, 0, "Event038", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event038Id, ProviderGuid);
            }
        }
        public const int Event039Id = 39;
        public event Action<EmptyTraceData> Event039
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, Event039Id, 0, "Event039", Guid.Empty, 0, "", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, Event039Id, ProviderGuid);
            }
        }
        #endregion
    }

    /// <summary>
    /// Test code to ensure that the TraceEventDispatcher works properly in the face of lots of
    /// adds and deletes.   
    /// </summary>
    public class DispatcherTester
    {
        public DispatcherTester(ITestOutputHelper output)
        {
            // Create a real time session, however we 
            m_dispatcher = new ETWTraceEventSource("TestDispatcher", TraceEventSourceType.Session);
            Output = output;
        }

        private ITestOutputHelper Output
        {
            get;
        }

        [Fact]
        public void ObservableTests()
        {
            var parser = new TestParser(m_dispatcher);

            var originalCallBackCount = m_dispatcher.CallbackCount();

            IObservable<EmptyTraceData> events1 = parser.Observe<EmptyTraceData>("Event001");
            IObservable<TraceEvent> all = m_dispatcher.ObserveAll();
            IObservable<TraceEvent> unhandled = m_dispatcher.ObserveUnhandled();

            var callbackCount = m_dispatcher.DistinctCallbackCount();

            using (Subscribe(events1, d => Output.WriteLine("1 Got event 1"), () => Output.WriteLine("1 Completed")))
            using (Subscribe(events1, d => Output.WriteLine("2 Got event 1"), () => Output.WriteLine("2 Completed")))
            using (Subscribe(all, d => Output.WriteLine("ALL Got event NUM {0}", d.ID), () => Output.WriteLine("ALL Completed")))
            using (Subscribe(unhandled, d => Output.WriteLine("UNHANDLED Got event NUM {0}", d.ID), () => Output.WriteLine("UNHANDLED Completed")))
            {
                SendEvent(TestParser.ProviderGuid, 1);
                SendEvent(TestParser.ProviderGuid, 1);
                SendEvent(TestParser.ProviderGuid, 3);
            }

            var endCallbackCount = m_dispatcher.CallbackCount();
            Assert.Equal(originalCallBackCount, endCallbackCount);
            Console.WriteLine("Done");
        }

        [Fact]
        public void DispatcherTests()
        {
            const int IterationCount = 2000;

            var asParserServices = (ITraceParserServices)m_dispatcher;
            var curCallbackCount = m_dispatcher.DistinctCallbackCount();
            Output.WriteLine("Callback Count {0}", curCallbackCount);
            var r = new Random(10);

            bool verbose = false;
            for (int i = 1; i < IterationCount; i++)
            {
                if (i == -1 || i == -2)
                {
                    verbose = true;
                    Trace.WriteLine("**** ITERATION " + i);
                    Trace.WriteLine(Dump());
                    Trace.WriteLine(m_dispatcher.DumpHash());
                }
                Assert.True(m_dispatcher.TemplateLength() <= 1024);

                if (i % 128 == 0)
                {
                    Trace.WriteLine("Callback Count " + m_dispatcher.DistinctCallbackCount());
                    Trace.WriteLine("templates.Length " + m_dispatcher.TemplateLength());
                }
                if (m_activeTemplates.Count < 470 && (m_activeTemplates.Count == 0 || r.Next(100) < 51))
                {
                    if (r.Next(5) != 0 || m_activeTemplates.Count == 0)
                    {
                        var guid = new Guid(i, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

                        // sometimes add a brand new event
                        var newTemplate = new EmptyTraceData(MakeTarget(i), r.Next(256), 0, "MyTask" + i.ToString(), Guid.Empty, 0, "", guid, "Test");
                        m_activeTemplates.Add(newTemplate);
                        if (verbose)
                        {
                            Trace.WriteLine(string.Format("Adding new event {0} {1} {2}", newTemplate.GetHashCode(), newTemplate.ProviderGuid, newTemplate.eventID));
                        }

                        asParserServices.RegisterEventTemplate(newTemplate);
                        var newCallbackCount = m_dispatcher.DistinctCallbackCount();
                        Assert.Equal(curCallbackCount + 1, newCallbackCount);
                        curCallbackCount = newCallbackCount;
                    }
                    else
                    {
                        // sometimes create another client for the same event.  
                        var toCloneIdx = r.Next(m_activeTemplates.Count);
                        var toClone = m_activeTemplates[toCloneIdx];

                        var newTemplate = new EmptyTraceData(MakeTarget(i), (int)toClone.eventID, 0, "MyTask" + i.ToString(), Guid.Empty, 0, "", toClone.ProviderGuid, "Test");
                        m_repeatTemplates.Add(newTemplate);

                        if (verbose)
                        {
                            Trace.WriteLine(string.Format("cloning event {0} {1} {2}", newTemplate.GetHashCode(), newTemplate.ProviderGuid, newTemplate.eventID));
                        }

                        asParserServices.RegisterEventTemplate(newTemplate);
                        var newCallbackCount = m_dispatcher.DistinctCallbackCount();
                        Assert.Equal(curCallbackCount, newCallbackCount);
                        curCallbackCount = newCallbackCount;
                    }
                }
                else
                {
                    // Remove an entry at random.
                    var toRemoveIdx = r.Next(m_activeTemplates.Count + m_repeatTemplates.Count);
                    EmptyTraceData toRemove;
                    if (toRemoveIdx < m_activeTemplates.Count)
                    {
                        toRemove = m_activeTemplates[toRemoveIdx];
                        m_activeTemplates.RemoveAt(toRemoveIdx);

                        // If there are any repeated entries make it a new active.  
                        var replacementIdx = m_repeatTemplates.FindIndex(template => template.ProviderGuid == toRemove.ProviderGuid && template.eventID == toRemove.eventID);
                        if (0 <= replacementIdx)
                        {
                            m_activeTemplates.Add(m_repeatTemplates[replacementIdx]);
                            m_repeatTemplates.RemoveAt(replacementIdx);
                        }
                    }
                    else
                    {
                        toRemoveIdx -= m_activeTemplates.Count;
                        toRemove = m_repeatTemplates[toRemoveIdx];
                        m_repeatTemplates.RemoveAt(toRemoveIdx);
                    }

                    if (verbose)
                    {
                        Trace.WriteLine(string.Format("Removing event {0} {1} {2}", toRemove.GetHashCode(), toRemove.ProviderGuid, toRemove.eventID));
                    }

                    m_inactiveTemplates.Add(toRemove);
                    asParserServices.UnregisterEventTemplate(toRemove.Target, (int)toRemove.eventID, toRemove.ProviderGuid);

                    curCallbackCount = m_dispatcher.DistinctCallbackCount();
                }
                RunTest();
            }
            // Remove all the rest.  
            foreach (var toRemove in m_activeTemplates)
            {
                asParserServices.UnregisterEventTemplate(toRemove.Target, (int)toRemove.eventID, toRemove.ProviderGuid);
            }

            foreach (var toRemove in m_repeatTemplates)
            {
                asParserServices.UnregisterEventTemplate(toRemove.Target, (int)toRemove.eventID, toRemove.ProviderGuid);
            }

            Output.WriteLine("Callback Count {0}", m_dispatcher.DistinctCallbackCount());
        }

        private unsafe void SendEvent(Guid providerGuid, int eventID, bool isClassic = false)
        {
            TraceEventNativeMethods.EVENT_RECORD rawDataStorage;
            TraceEventNativeMethods.EVENT_RECORD* rawData = &rawDataStorage;
            rawData->EventHeader.ProviderId = providerGuid;
            rawData->EventHeader.Id = (ushort)eventID;
            rawData->EventHeader.Flags = 0;
            if (isClassic)
            {
                rawData->EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_CLASSIC_HEADER;
            }

            var event_ = m_dispatcher.Lookup(rawData);
            m_dispatcher.Dispatch(event_);
        }

        public string Dump()
        {
            var sw = new StringWriter();

            sw.WriteLine("Active");
            foreach (var key in m_activeTemplates)
            {
                sw.WriteLine("  {0} {1} {2}", key.GetHashCode(), key.ProviderGuid, key.eventID);
            }

            sw.WriteLine("");

            sw.WriteLine("Repeat");
            foreach (var key in m_repeatTemplates)
            {
                sw.WriteLine("  {0} {1} {2}", key.GetHashCode(), key.ProviderGuid, key.eventID);
            }

            sw.WriteLine("");

            sw.WriteLine("Visited");
            foreach (var key in m_visited)
            {
                sw.WriteLine("  {0} {1} {2}", key.GetHashCode(), key.ProviderGuid, key.eventID);
            }

            return sw.ToString();
        }

        private class MyObserver<T> : IObserver<T>
        {
            public MyObserver(Action<T> action, Action completed = null) { m_action = action; m_completed = completed; }
            public void OnNext(T value) { m_action(value); }
            public void OnCompleted()
            {
                if (m_completed != null)
                {
                    m_completed();
                }
            }
            public void OnError(Exception error) { }

            private Action<T> m_action;
            private Action m_completed;
        }

        private static IDisposable Subscribe<T>(IObservable<T> observable, Action<T> action, Action completed = null)
        {
            return observable.Subscribe(new MyObserver<T>(action, completed));
        }

        private void RunTest()
        {
            m_visited.Clear();

            // Send an event to every active template
            foreach (var template in m_activeTemplates)
            {
                SendEvent(template.ProviderGuid, (int)template.eventID, template.lookupAsClassic);
            }

            foreach (var template in m_inactiveTemplates)
            {
                Assert.True(!m_visited.Contains(template));
            }

            foreach (var template in m_activeTemplates)
            {
                Assert.Contains(template, m_visited);
            }

            foreach (var template in m_repeatTemplates)
            {
                Assert.Contains(template, m_visited);
            }
        }

        private Action<EmptyTraceData> MakeTarget(int i)
        {
            return delegate (EmptyTraceData data) { m_dummy = i; Target(data); };
        }

        private void Target(EmptyTraceData data)
        {
            Assert.True(m_visited.Add(data));
        }

        private int m_dummy;
        private TraceEventDispatcher m_dispatcher;
        private HashSet<EmptyTraceData> m_visited = new HashSet<EmptyTraceData>();
        private List<EmptyTraceData> m_inactiveTemplates = new List<EmptyTraceData>();
        private List<EmptyTraceData> m_activeTemplates = new List<EmptyTraceData>();
        private List<EmptyTraceData> m_repeatTemplates = new List<EmptyTraceData>();
    }
}
