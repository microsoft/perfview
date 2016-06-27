using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PerfViewExtensibility;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
////Microsoft.Diagnostics.Tracing.Parsers.Kernel;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace PerfDataService.Controllers
{
    [Route("api/[controller]/open")]
    public class DataController : Controller
    {
        // GET: api/data
        [HttpGet]
        public string Get()
        {
            // Dictionary containing entire hierarchy
            Dictionary<string, object> tree = new Dictionary<string, object>();
            tree.Add("status", "OK");

            // Top level items
            List<object> items = new List<object>();
            tree.Add("items", items);

            // First top level item
            Dictionary<string, object> item = new Dictionary<string, object>();
            items.Add(item);
            item.Add("name", "TraceInfo");
            item.Add("type", "htmlReport");

            // Second top level item
            item = new Dictionary<string, object>();
            items.Add(item);
            item.Add("name", "Cpu Stacks");
            item.Add("type", "stackViewer");

            // First top level folder
            item = new Dictionary<string, object>();
            items.Add(item);
            item.Add("name", "Test Folder 1");
            item.Add("type", "file");
            List<object> subItems = new List<object>();
            item.Add("items", subItems);

            // Folder subitem
            Dictionary<string, object> subItem = new Dictionary<string, object>();
            subItems.Add(subItem);
            subItem.Add("name", "Dump");
            subItem.Add("type", "otherType");

            // Folder subitem
            subItem = new Dictionary<string, object>();
            subItems.Add(subItem);
            subItem.Add("name", "Trace");
            subItem.Add("type", "otherType");

            // Third top level item
            item = new Dictionary<string, object>();
            items.Add(item);
            item.Add("name", "Another Top Level Item");
            item.Add("type", "otherType");

            string json = JsonConvert.SerializeObject(tree, Formatting.Indented);

            if (json == null)
            {
                // Form a response using HttpResponseException
            }

            string etlFileName = @"C:\Users\t-kahoop\Development\perfview\src\PerfView\bin\Debug\PerfViewData.etl.zip\PerfViewData";
            var etlFile = PerfViewExtensibility.CommandEnvironment.OpenETLFile(etlFileName);
            TraceEvents events = etlFile.TraceLog.Events;
            var traceEventSource = events.GetSource();
            traceEventSource.Process();

            return json;
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
