namespace TraceEventAPIServer
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Microsoft.Diagnostics.Tracing.Stacks;

    [DataContract]
    public class TreeNode
    {
        private readonly object lockObj = new object();

        private TreeNode[] callees;

        private readonly CallTreeNode backingNodeWithChildren;

        internal TreeNode()
        {
        }

        public TreeNode(CallTreeNodeBase template)
        {
            if (template == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(template));
            }

            this.Id = template.Name;
            this.ContextId = this.Id;
            this.ParentContextId = string.Empty;
            this.ParentId = string.Empty;
            this.Name = template.Name;
            this.InclusiveMetric = template.InclusiveMetric;
            this.InclusiveCount = template.InclusiveCount;
            this.ExclusiveMetric = template.ExclusiveMetric;
            this.ExclusiveCount = template.ExclusiveCount;
            this.ExclusiveFoldedMetric = template.ExclusiveFoldedMetric;
            this.ExclusiveFoldedCount = template.ExclusiveFoldedCount;
            this.FirstTimeRelativeMSec = template.FirstTimeRelativeMSec;
            this.LastTimeRelativeMSec = template.LastTimeRelativeMSec;
            this.InclusiveMetricPercent = template.InclusiveMetric * 100 / template.CallTree.PercentageBasis;
            this.ExclusiveMetricPercent = template.ExclusiveMetric * 100 / template.CallTree.PercentageBasis;
            this.ExclusiveFoldedMetricPercent = template.ExclusiveFoldedMetric * 100 / template.CallTree.PercentageBasis;
            this.HasChildren = false;
            this.BackingNode = template;
        }

        public TreeNode(CallTreeNode template)
        {
            if (template == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(template));
            }

            this.ParentContextId = string.Empty;
            this.Id = template.Name;
            this.ParentId = string.Empty;
            this.ContextId = this.Id;
            this.Name = template.Name;
            this.InclusiveMetric = template.InclusiveMetric;
            this.InclusiveCount = template.InclusiveCount;
            this.ExclusiveMetric = template.ExclusiveMetric;
            this.ExclusiveCount = template.ExclusiveCount;
            this.ExclusiveFoldedMetric = template.ExclusiveFoldedMetric;
            this.ExclusiveFoldedCount = template.ExclusiveFoldedCount;
            this.FirstTimeRelativeMSec = template.FirstTimeRelativeMSec;
            this.LastTimeRelativeMSec = template.LastTimeRelativeMSec;
            this.InclusiveMetricPercent = template.InclusiveMetric * 100 / template.CallTree.PercentageBasis;
            this.ExclusiveMetricPercent = template.ExclusiveMetric * 100 / template.CallTree.PercentageBasis;
            this.ExclusiveFoldedMetricPercent = template.ExclusiveFoldedMetric * 100 / template.CallTree.PercentageBasis;
            this.HasChildren = template.HasChildren;
            this.BackingNode = template;
            this.backingNodeWithChildren = template;
        }

        public CallTreeNodeBase BackingNode { get; }

        [DataMember]
        public string ParentContextId { get; set; }

        [DataMember]
        public string ContextId { get; set; }

        [DataMember]
        public string ParentId { get; set; }

        [DataMember]
        public string Id { get; set; }

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
        public float InclusiveMetricPercent { get; set; }

        [DataMember]
        public float ExclusiveMetricPercent { get; set; }

        [DataMember]
        public float ExclusiveFoldedMetricPercent { get; set; }

        [DataMember]
        public double FirstTimeRelativeMSec { get; set; }

        [DataMember]
        public double LastTimeRelativeMSec { get; set; }

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
                            this.callees[i] = new TreeNode(backingNodeCallees[i]) { ContextId = this.ContextId + "/" + i, ParentContextId = this.ContextId, ParentId = this.Name }; // for example, 7105/0 .. 7105/N
                        }
                    }

                    return this.callees;
                }
            }
        }
    }
}