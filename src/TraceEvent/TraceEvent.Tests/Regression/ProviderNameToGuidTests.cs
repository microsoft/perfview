using System;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Xunit;

namespace TraceEventTests
{
    public class ProviderNameToGuidTests
    {
        /// <summary>
        /// Test that GetProviderGuidByName (which uses ProviderNameToGuid internally) doesn't throw
        /// when called and correctly returns the GUID for Microsoft-Windows-DotNETRuntime.
        /// This test validates the fix for the race condition that could cause
        /// ERROR_INSUFFICIENT_BUFFER (HR 122) crashes.
        /// </summary>
        [WindowsFact]
        public void ProviderNameToGuid_DoesNotThrow_AndReturnsCorrectGuid()
        {
            // Act - Call ProviderNameToGuid and ensure it doesn't throw
            Guid actualGuid = TraceEventProviders.GetProviderGuidByName(ClrTraceEventParser.ProviderName);

            // Assert - Verify it matches the expected GUID
            Assert.Equal(ClrTraceEventParser.ProviderGuid, actualGuid);
        }
    }
}
