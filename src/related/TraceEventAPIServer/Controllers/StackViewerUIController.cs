namespace TraceEventAPIServer.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using TraceEventAPIServer.Models;
    using System.Net.Http;

    public sealed class StackViewerUIController : Controller
    {
        //private readonly IServerAddressesFeature serverAddressesFeature;

        //public StackViewerUIController(IServerAddressesFeature serverAddressesFeature)
        //{
        //    this.serverAddressesFeature = serverAddressesFeature;
        //}

        [Route("ui/stackviewer/summary", Name = "HotSpots")]
        public ActionResult Hotspots(StackViewerViewModel model)
        {
            if (string.IsNullOrEmpty(model.Filename))
            {
                throw new ArgumentNullException("filename");
            }

            if (string.IsNullOrEmpty(model.StackType))
            {
                throw new ArgumentNullException("stacktype");
            }

            model.TreeNodes = JsonConvert.DeserializeObject<List<TreeNode>>(new HttpClient().GetStringAsync($"{this.LookupHostname()}/stackviewer/summary?{model}&numNodes=100").Result);

            this.ViewBag.Title = "Hotspots Viewer";
            return this.View(model);
        }

        [Route("ui/stackviewer/callertree", Name = "Callers", Order = 2)]
        public ActionResult Callers(CallersViewStackViewerViewModel model, string name)
        {
            if (string.IsNullOrEmpty(model.Filename))
            {
                throw new ArgumentNullException("filename");
            }

            if (string.IsNullOrEmpty(model.StackType))
            {
                throw new ArgumentNullException("stacktype");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Contains("&"))
            {
                name = name.Replace("&", "%26");
            }

            this.ViewBag.Title = "Callers Viewer";

            string key = this.LookupHostname();
            model.Node = JsonConvert.DeserializeObject<TreeNode>(new HttpClient().GetStringAsync($"{key}/stackviewer/node?{model}&name={name}").Result);
            model.TreeNodes = JsonConvert.DeserializeObject<List<TreeNode>>(new HttpClient().GetStringAsync($"{key}/stackviewer/callertree?{model}&name={name}").Result);

            return this.View(model);
        }

        [Route("ui/stackviewer/callertree/children", Name = "CallersChildren", Order = 1)]
        public ActionResult CallersChildren(CallersViewStackViewerViewModel model, string name, string path)
        {
            if (string.IsNullOrEmpty(model.Filename))
            {
                throw new ArgumentNullException("filename");
            }

            if (string.IsNullOrEmpty(model.StackType))
            {
                throw new ArgumentNullException("stacktype");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Contains("&"))
            {
                name = name.Replace("&", "%26");
            }

            model.TreeNodes = JsonConvert.DeserializeObject<List<TreeNode>>(new HttpClient().GetStringAsync($"{this.LookupHostname()}/stackviewer/callertree?{model}&name={name}&path={path}").Result);

            this.ViewBag.Title = "Callers Viewer";
            return this.View(model);
        }

        [Route("ui/stackviewer/source/callertree", Name = "SourceViewer")]
        public ActionResult SourceViewer(CallersViewStackViewerViewModel model, string name, string path)
        {
            if (string.IsNullOrEmpty(model.Filename))
            {
                throw new ArgumentNullException("filename");
            }

            if (string.IsNullOrEmpty(model.StackType))
            {
                throw new ArgumentNullException("stacktype");
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Contains("&"))
            {
                name = name.Replace("&", "%26");
            }

            return this.View(JsonConvert.DeserializeObject<SourceInformation>(new HttpClient().GetStringAsync($"{this.LookupHostname()}/stackviewer/source/callertree?{model}&name={name}&path={path}").Result));
        }
        
        private string LookupHostname()
        {
            return "http://localhost:50001";
        }
    }
}