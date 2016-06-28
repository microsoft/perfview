using System;
using System.IO;
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
        public string Get([FromQuery]string path)
        {
            // Make sure the user provided a non-null query string parameter 'path'
            if (String.IsNullOrEmpty(path))
            {
                return null;  // TODO: Form proper respone notifying user that the path passed was nonexistent
            }

            /* Create Dictionary to be returned as JSON in response */

            // Dictionary containing entire hierarchy
            Dictionary<string, object> tree = new Dictionary<string, object>();
            tree.Add("status", "OK");
            tree.Add("path", path);

            // Top level items
            List<object> items = new List<object>();
            tree.Add("items", items);

            /* Identify path target */

            // Does this path lead to a directory or a file?
            if (Directory.Exists(path))
            {
                // Indiciate (in the JSON response object) that this is a directory
                tree.Add("type", "directory");

                // Get all children of directory
                IEnumerable<string> children = Directory.EnumerateFileSystemEntries(path);

                // Package into JSON
                foreach (string child in children)
                {
                    // Set name property
                    Dictionary<string, object> directory = new Dictionary<string, object>();
                    directory.Add("name", child.Split('/').Last());  // Name of item
                    //directory.Add("type", GetType(child));  // TODO: Create method to derive the type of a child item
                    directory.Add("path", child);  // Path to item
                    //directory.Add("hasChildren", hasChildren(child));  // TODO: Create method to derive hasChildren property of child item
                }
            }
            else if (new FileInfo(path).Exists)
            {
                // Create .etl file from path (assume only .etl.zip for now)
                string etlFileName = path;
                var etlFile = PerfViewExtensibility.CommandEnvironment.OpenETLFile(etlFileName);
                TraceEvents events = etlFile.TraceLog.Events;

                Stacks CPUStacks = etlFile.CPUStacks();

                // First top level item
                Dictionary<string, object> item = new Dictionary<string, object>();
                items.Add(item);
                item.Add("name", etlFileName);
                item.Add("type", etlFile.GetType());
                System.Diagnostics.Debug.WriteLine("type: " + etlFile.GetType());

                // Second top level item
                item = new Dictionary<string, object>();
                items.Add(item);
                item.Add("name", CPUStacks.Name);
                item.Add("type", CPUStacks.GetType());
                System.Diagnostics.Debug.WriteLine("type: " + CPUStacks.GetType());
            }
            else
            {
                // Invalid path
                return null;  // TODO: Form proper response notifying user that the path passed was incorrect
            }

            //// First top level folder
            //item = new Dictionary<string, object>();
            //items.Add(item);
            //item.Add("name", "Test Folder 1");
            //item.Add("type", "file");
            //List<object> subItems = new List<object>();
            //item.Add("items", subItems);

            //// Folder subitem
            //Dictionary<string, object> subItem = new Dictionary<string, object>();
            //subItems.Add(subItem);
            //subItem.Add("name", "Dump");
            //subItem.Add("type", "otherType");

            //// Folder subitem
            //subItem = new Dictionary<string, object>();
            //subItems.Add(subItem);
            //subItem.Add("name", "Trace");
            //subItem.Add("type", "otherType");

            //// Third top level item
            //item = new Dictionary<string, object>();
            //items.Add(item);
            //item.Add("name", "Another Top Level Item");
            //item.Add("type", "otherType");

            string json = JsonConvert.SerializeObject(tree, Formatting.Indented);

            if (json == null)
            {
                // Form a response using HttpResponseException
            }

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
