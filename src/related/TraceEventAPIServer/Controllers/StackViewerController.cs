namespace TraceEventAPIServer.Controllers
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc;
    using Models;

    public sealed class StackViewerController
    {
        private readonly ICallTreeDataProvider dataProvider;

        public StackViewerController(ICallTreeDataProviderFactory dataProviderFactory)
        {
            if (dataProviderFactory == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(dataProviderFactory));
            }

            this.dataProvider = dataProviderFactory.Get();
        }

        [HttpGet]
        [Route("stackviewer/summary")]
        public List<TreeNode> Get(int numNodes)
        {
            return this.dataProvider.GetSummaryTree(numNodes);
        }

        [HttpGet]
        [Route("stackviewer/node")]
        public TreeNode Node(string name)
        {
            return this.dataProvider.GetNode(name);
        }

        [HttpGet]
        [Route("stackviewer/callertree")]
        public TreeNode[] CallerTree(string name)
        {
            return this.dataProvider.GetCallerTree(name);
        }

        [HttpGet]
        [Route("stackviewer/callertree")]
        public TreeNode[] CallerTree(string name, string path)
        {
            return this.dataProvider.GetCallerTree(name, path);
        }

        [HttpGet]
        [Route("stackviewer/calleetree")]
        public TreeNode[] CalleeTree(string name)
        {
            return this.dataProvider.GetCalleeTree(name);
        }

        [HttpGet]
        [Route("stackviewer/calleetree")]
        public TreeNode[] CalleeTree(string name, string path)
        {
            return this.dataProvider.GetCalleeTree(name, path);
        }

        [HttpGet]
        [Route("stackviewer/source")]
        public SourceInformation Source(string name)
        {
            return this.dataProvider.Source(this.dataProvider.GetNode(name));
        }

        [HttpGet]
        [Route("stackviewer/source/caller")]
        public SourceInformation CallerContextSource(string name, string path)
        {
            return this.dataProvider.Source(this.dataProvider.GetCallerTreeNode(name, path));
        }

        [HttpGet]
        [Route("stackviewer/source/callee")]
        public SourceInformation CalleeContextSource(string name, string path)
        {
            return this.dataProvider.Source(this.dataProvider.GetCalleeTreeNode(name, path));
        }
    }
}