using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Tracing.StackSources;
using Xunit;

namespace TraceEventTests
{
    public class StraceStackSourceTests
    {
        [Theory]
        [InlineData("dotnet-info.strace.zip")]
        [InlineData("ls.strace.zip")]
        [InlineData("perf.strace.zip")]
        public void VerifyTraces(string zippedTraceFileName)
        {
            string fileToUnzip = Path.Combine("inputs", "strace", zippedTraceFileName);
            string unzippedFile = Path.ChangeExtension(fileToUnzip, string.Empty);

            if (File.Exists(unzippedFile))
            {
                File.Delete(unzippedFile);
            }
            ZipFile.ExtractToDirectory(fileToUnzip, Path.GetDirectoryName(fileToUnzip));

            string baselineFile = Path.ChangeExtension(fileToUnzip, "perfview.xml");
            if (!File.Exists(baselineFile))
            {
                // Create the baseline and fail the test.
                string newBaselineFilePath = Path.ChangeExtension(fileToUnzip, "perfview.xml");
                StraceStackSource stackSource = new StraceStackSource(unzippedFile);
                XmlStackSourceWriter.WriteStackViewAsXml(stackSource, newBaselineFilePath);
                Assert.Fail($"Baseline file does not exist.  Created new baseline file at {newBaselineFilePath}.  Confirm that this file is correct, and then save it in the inputs\\strace directory of the repo.");
            }

            // Open the baseline.
            XmlStackSource baselineStackSource = new XmlStackSource(baselineFile);
            FilterStackSource baselineFilterStackSource = new FilterStackSource(new FilterParams(), baselineStackSource, ScalingPolicyKind.TimeMetric);
            CallTree baselineCallTree = new CallTree(ScalingPolicyKind.TimeMetric)
            {
                StackSource = baselineFilterStackSource
            };

            // Open the strace file.
            StraceStackSource straceStackSource = new StraceStackSource(unzippedFile);
            FilterStackSource straceFilterStackSource = new FilterStackSource(new FilterParams(), straceStackSource, ScalingPolicyKind.TimeMetric);
            CallTree straceCallTree = new CallTree(ScalingPolicyKind.TimeMetric)
            {
                StackSource = straceFilterStackSource
            };

            // Compare the lists of ByName frames.
            List<CallTreeNodeBase> baselineByName = baselineCallTree.ByIDSortedExclusiveMetric();
            baselineByName.Sort((x, y) => x.DisplayName.CompareTo(y.DisplayName));
            List<CallTreeNodeBase> straceByName = straceCallTree.ByIDSortedExclusiveMetric();
            straceByName.Sort((x, y) => x.DisplayName.CompareTo(y.DisplayName));

            Assert.Equal(baselineByName.Count, straceByName.Count);

            for (int i = 0; i < baselineByName.Count; i++) {
                CallTreeNodeBase baselineNode = baselineByName[i];
                CallTreeNodeBase straceNode = straceByName[i];
                Assert.Equal(baselineNode.DisplayName, straceNode.DisplayName);
                Assert.Equal(baselineNode.FirstTimeRelativeMSec, straceNode.FirstTimeRelativeMSec, 3);
                Assert.Equal(baselineNode.LastTimeRelativeMSec, straceNode.LastTimeRelativeMSec, 3);
                Assert.Equal(baselineNode.ExclusiveMetric, straceNode.ExclusiveMetric);
                Assert.Equal(baselineNode.ExclusiveCount, straceNode.ExclusiveCount, 3);
                Assert.Equal(baselineNode.InclusiveMetric, straceNode.InclusiveMetric);
                Assert.Equal(baselineNode.InclusiveCount, straceNode.InclusiveCount, 3);
            }
        }
    }
}