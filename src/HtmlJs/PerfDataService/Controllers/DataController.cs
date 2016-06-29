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
            path = cleanUpPath(path);
            if (path == null) { return null; }  // TODO: Form proper response

            /* Create Dictionary to be returned as JSON in response */

            // Dictionary containing entire hierarchy
            Dictionary<string, object> tree = new Dictionary<string, object>();
            tree.Add("status", "OK");
            tree.Add("text", path.Split(new Char[] {'/', '\\'}).Last());
            tree.Add("path", path);
            tree.Add("type", getTypeOfItem(path));

            try
            {
                List<object> children = getChildrenForPath(path);
                tree.Add("children", children);
            } catch (UnauthorizedAccessException)
            {
                // TODO: Form a proper response for this
            }

            string json = JsonConvert.SerializeObject(tree, Formatting.Indented);

            if (json == null)
            {
                // TODO: Form a response using HttpResponseException
            }

            return json;
        }


        public string cleanUpPath(string path)
        {
            // Make sure the user provided a non-null query string parameter 'path'
            if (String.IsNullOrEmpty(path))
            {
                return null;  // TODO: Form proper respone notifying user that the path passed was nonexistent
            }

            if (path.Last() == '/' && !path.Equals("C:/") && !path.Equals("C:\\")) {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }


        public List<object> getChildrenForPath(string pathToItem)
        {
            List<object> childrenContainer = new List<object>();

            /* IF DIRECTORY */
            if (Directory.Exists(pathToItem))
            {
                // Get all children in the directory
                IEnumerable<string> children = Directory.EnumerateFileSystemEntries(pathToItem);

                if (pathToItem != "C:/" || pathToItem != "C:\\")
                {
                    // Add '..' directory
                    Dictionary<string, object> upDir = new Dictionary<string, object>();
                    upDir.Add("text", "..");  // Name of item
                    upDir.Add("type", "dir");  // Type of item
                    upDir.Add("path", System.IO.Directory.GetParent(pathToItem).FullName);  // Path to item
                    upDir.Add("hasChildren", false);  // TODO: Create method to derive hasChildren property of child item
                    childrenContainer.Add(upDir);
                }

                // Package them into dictionary objects
                foreach (string child in children)
                {
                    string type = getTypeOfItem(child);
                    if (type == null) { continue; }  // Unsupported file types return null

                    // This is a file type we want to return!
                    Dictionary<string, object> dir = new Dictionary<string, object>();
                    dir.Add("text", child.Split('\\').Last());  // Name of item
                    dir.Add("type", type);  // Type of item
                    dir.Add("path", child);  // Path to item
                    dir.Add("hasChildren", hasChildren(child));  // TODO: Create method to derive hasChildren property of child item
                    childrenContainer.Add(dir);
                }

                System.Diagnostics.Debug.WriteLine("\n\n\n" + children.ToString() + "\n\n\n");

                return childrenContainer;
            }
            // IF REAL FILE (e.g. .etl.zip, .etl)
            else if (new FileInfo(pathToItem).Exists)
            {
                // Assume only .etl.zip for now
                string etlFileName = pathToItem;
                var etlFile = PerfViewExtensibility.CommandEnvironment.OpenETLFile(etlFileName);
                TraceEvents events = etlFile.TraceLog.Events;

                // Create CPU Stacks as child
                Stacks CPUStacks = etlFile.CPUStacks();

                Dictionary<string, object> child = new Dictionary<string, object>();
                childrenContainer.Add(child);
                child.Add("text", CPUStacks.Name);
                child.Add("type", CPUStacks.GetType());
                System.Diagnostics.Debug.WriteLine("type: " + CPUStacks.GetType());

                // TODO: Create Process / Files / Registry Stacks as child
                // TODO: Create TraceInfo htmlReport as child
                // TODO: Create Processes htmlReport as child
                // TODO: Create Events as child
                // TODO: Create Memory Group as child
                // TODO: Create Advanced Group as child

                return childrenContainer;
            }
            // IF PERFVIEW FILE (e.g. "stacks", "events", "html", "gcdump")
            else
            {
                // Virtual path
                // TODO: Actually, make this another GET route on the API
                return childrenContainer;
            }
        }


        public string getTypeOfItem(string itemPath)
        {
            if (Directory.Exists(itemPath))
            {
                return "dir";
            }
            else if (new FileInfo(itemPath).Exists)
            {
                string fileExtension = itemPath.Split('.').Last();

                // Add more types as they are implemented
                switch (fileExtension)
                {
                    case "zip":
                        return "file";
                    case "etl":
                        return "file";
                    default:
                        // This file type is currently unsupported
                        return null;
                }
            }

            // Something went wrong between this point and the discovery of the item
            return null;
        }


        public Boolean hasChildren(string itemPath)
        {
            if (Directory.Exists(itemPath) && Directory.EnumerateFileSystemEntries(itemPath).Count() > 0)
            {
                // It's a directory and we have confirmed that it has children
                return true;
            }
            else if (new FileInfo(itemPath).Exists)
            {
                string type = getTypeOfItem(itemPath);

                // It's a file, and we must check that it's a type we expect to have children
                // Add more types as they are implemented
                switch (type)
                {
                    case "file":
                        return true;
                    default:
                        // This file type is currently unsupported
                        return false;
                }
            }

            return false;
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
