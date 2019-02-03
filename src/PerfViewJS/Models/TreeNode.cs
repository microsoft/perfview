// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Text;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Diagnostics.Tracing.Stacks;

    [DataContract]
    public class TreeNode
    {
        private readonly object lockObj = new object();

        private readonly CallTreeNode backingNodeWithChildren;

        private TreeNode[] callees;

        public TreeNode(CallTreeNodeBase template)
        {
            if (template == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(template));
            }

            this.Base64EncodedId = Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(template.Name));
            this.Path = string.Empty;
            this.Name = template.Name;
            this.InclusiveMetric = template.InclusiveMetric;
            this.InclusiveCount = template.InclusiveCount;
            this.ExclusiveMetric = template.ExclusiveMetric;
            this.ExclusiveCount = template.ExclusiveCount;
            this.ExclusiveFoldedMetric = template.ExclusiveFoldedMetric;
            this.ExclusiveFoldedCount = template.ExclusiveFoldedCount;
            this.InclusiveMetricByTimeString = template.InclusiveMetricByTimeString;
            this.FirstTimeRelativeMSec = template.FirstTimeRelativeMSec.ToString("N3");
            this.LastTimeRelativeMSec = template.LastTimeRelativeMSec.ToString("N3");
            this.InclusiveMetricPercent = (template.InclusiveMetric * 100 / template.CallTree.PercentageBasis).ToString("N2");
            this.ExclusiveMetricPercent = (template.ExclusiveMetric * 100 / template.CallTree.PercentageBasis).ToString("N2");
            this.ExclusiveFoldedMetricPercent = (template.ExclusiveFoldedMetric * 100 / template.CallTree.PercentageBasis).ToString("N2");
            this.HasChildren = false;
            this.BackingNode = template;
        }

        public TreeNode(CallTreeNode template)
        {
            if (template == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(template));
            }

            this.Path = string.Empty;
            this.Base64EncodedId = Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(template.Name));
            this.Name = template.Name;
            this.InclusiveMetric = template.InclusiveMetric;
            this.InclusiveCount = template.InclusiveCount;
            this.ExclusiveMetric = template.ExclusiveMetric;
            this.ExclusiveCount = template.ExclusiveCount;
            this.ExclusiveFoldedMetric = template.ExclusiveFoldedMetric;
            this.ExclusiveFoldedCount = template.ExclusiveFoldedCount;
            this.InclusiveMetricByTimeString = template.InclusiveMetricByTimeString;
            this.FirstTimeRelativeMSec = template.FirstTimeRelativeMSec.ToString("N3");
            this.LastTimeRelativeMSec = template.LastTimeRelativeMSec.ToString("N3");
            this.InclusiveMetricPercent = (template.InclusiveMetric * 100 / template.CallTree.PercentageBasis).ToString("N2");
            this.ExclusiveMetricPercent = (template.ExclusiveMetric * 100 / template.CallTree.PercentageBasis).ToString("N2");
            this.ExclusiveFoldedMetricPercent = (template.ExclusiveFoldedMetric * 100 / template.CallTree.PercentageBasis).ToString("N2");
            this.HasChildren = template.HasChildren;
            this.BackingNode = template;
            this.backingNodeWithChildren = template;
        }

        internal TreeNode()
        {
        }

        public CallTreeNodeBase BackingNode { get; }

        [DataMember]
        public string Path { get; set; }

        [DataMember]
        public string Base64EncodedId { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public float InclusiveMetric { get; set; }

        [DataMember]
        public float ExclusiveMetric { get; set; }

        [DataMember]
        public float ExclusiveFoldedMetric { get; set; }

        [DataMember]
        public float InclusiveCount { get; set; }

        [DataMember]
        public float ExclusiveCount { get; set; }

        [DataMember]
        public float ExclusiveFoldedCount { get; set; }

        [DataMember]
        public string InclusiveMetricPercent { get; set; }

        [DataMember]
        public string ExclusiveMetricPercent { get; set; }

        [DataMember]
        public string ExclusiveFoldedMetricPercent { get; set; }

        [DataMember]
        public string InclusiveMetricByTimeString { get; set; }

        [DataMember]
        public string FirstTimeRelativeMSec { get; set; }

        [DataMember]
        public string LastTimeRelativeMSec { get; set; }

        [DataMember]
        public double DurationMSec { get; set; }

        [DataMember]
        public bool HasChildren { get; set; }

        public TreeNode[] Children
        {
            get
            {
                lock (this.lockObj)
                {
                    if (this.callees == null && this.HasChildren)
                    {
                        IList<CallTreeNode> backingNodeCallees = this.backingNodeWithChildren.Callees;
                        int count = backingNodeCallees.Count;
                        this.callees = new TreeNode[count];

                        for (int i = 0; i < count; ++i)
                        {
                            this.callees[i] = new TreeNode(backingNodeCallees[i])
                            {
                                Path = string.IsNullOrEmpty(this.Path) ? i.ToString() : this.Path + '-' + i,
                            }; // for example, 7105/0 .. 7105/N
                        }
                    }

                    return this.callees;
                }
            }
        }
    }
}
