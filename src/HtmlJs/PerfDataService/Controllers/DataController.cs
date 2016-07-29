using System;
using System.IO;
using System.Net;
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

    public class DataController : Controller
    {
        string[] logicalDrives;

        // GET: api/data
        [HttpGet]
        [Route("api/[controller]/open")]
        public string Get([FromQuery]string path)
        {
            logicalDrives = Environment.GetLogicalDrives();
            if (string.IsNullOrEmpty(path)) { path = PerfDataService.Startup.config["HomeDirectory"]; }
            path = cleanUpPath(path);

            /* Create Dictionary to be returned as JSON in response */
            // Dictionary containing entire hierarchy
            Dictionary<string, object> tree = new Dictionary<string, object>();
            tree.Add("status", "OK");
            if (logicalDrives.Contains(path.ToUpper())) {
                tree.Add("text", path);
            } else
            {
                tree.Add("text", Path.GetFileName(path));  //.Split(new Char[] {'/', '\\'}).Last());
            }
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

            if (string.IsNullOrEmpty(json))
            {
                return "[]";
            }
            else
            {
                return json;
            }
        }


        [HttpGet]
        [Route("/api/[controller]/stackviewer/summary")]
        public string GetStackSummary([FromQuery]string filename, [FromQuery]string stackType)  // TODO: Remove these parameters
        {
            // Ensure the required properties are present
            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(stackType))
            {
                // TODO: Form proper resposne
                return null;
            }

            using (var client = new WebClient())
            {
                var url = "http://localhost:5000/stackviewer/summary" + Request.QueryString;
                var json = client.DownloadString(url);

                if (string.IsNullOrEmpty(json))
                {
                    return "[]";
                }
                else
                {
                    return json;
                }
            }
        }


        [HttpGet]
        [Route("/api/[controller]/stackviewer/node")]
        public string GetNode([FromQuery]string filename, [FromQuery]string name, [FromQuery]string stackType)  // TODO: Remove these parameters
        {
            // Ensure the required properties are present
            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(stackType) || string.IsNullOrEmpty(name))
            {
                // TODO: Form proper resposne
                return null;
            }

            using (var client = new WebClient())
            {
                var url = "http://localhost:5000/stackviewer/node" + Request.QueryString.Value;
                var json = client.DownloadString(url);

                if (string.IsNullOrEmpty(json))
                {
                    return "[]";
                }
                else
                {
                    return json;
                }
            }
        }


        [HttpGet]
        [Route("/api/[controller]/stackviewer/callertree")]
        public string GetCallers([FromQuery]string filename, [FromQuery]string name, [FromQuery]string stackType)  // TODO: Remove these parameters
        {
            // Ensure the required properties are present
            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(stackType) || string.IsNullOrEmpty(name))
            {
                // TODO: Form proper resposne
                return null;
            }

            using (var client = new WebClient())
            {
                var url = "http://localhost:5000/stackviewer/callertree" + Request.QueryString.Value;
                var json = client.DownloadString(url);

                if (string.IsNullOrEmpty(json))
                {
                    return "[]";
                }
                else
                {
                    return json;
                }
            }
        }


        [HttpGet]
        [Route("/api/[controller]/stackviewer/calleetree")]
        public string GetCallees([FromQuery]string filename, [FromQuery]string name, [FromQuery]string stackType)  // TODO: Remove these parameters
        {
            // Ensure the required properties are present
            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(stackType) || string.IsNullOrEmpty(name))
            {
                // TODO: Form proper resposne
                return null;
            }

            using (var client = new WebClient())
            {
                var url = "http://localhost:5000/stackviewer/calleetree" + Request.QueryString.Value;
                var json = client.DownloadString(url);
                
                if (string.IsNullOrEmpty(json))
                {
                    return "[]";
                } else
                {
                    return json;
                }
            }
        }


        public string cleanUpPath(string path)
        {
            string possibleDrive = Path.GetFullPath(path).ToUpper();
            if (path.Last() == '/' && !logicalDrives.Contains(possibleDrive)) {
                path = path.Substring(0, path.Length - 1);
            }
            else if (logicalDrives.Contains(possibleDrive))
            {
                return possibleDrive;
            }

            return path;
        }


        public List<object> getChildrenForPath(string pathToItem)
        {
            List<object> childrenContainer = new List<object>();

            /* IF DIRECTORY */
            if (Directory.Exists(pathToItem))
            {
                // Get all children in the directory, if applicable
                IEnumerable<string> children;
                try
                {
                    // This will succeed if the directory has subdirectories or files to enumerate over
                    children = Directory.EnumerateFileSystemEntries(pathToItem);
                }
                catch
                {
                    // This means the directory has no children, so we're going to create an empty list
                    children = new List<string>();
                }

                if (!logicalDrives.Contains(pathToItem.ToUpper()))  // TODO: Use Path.GetRootDirectory (store it in a global property)
                {
                    // Add '..' directory
                    Dictionary<string, object> upDir = new Dictionary<string, object>();
                    upDir.Add("text", "..");  // Name of item
                    upDir.Add("type", "dir");  // Type of item
                    if (!logicalDrives.Contains(pathToItem.ToUpper()))
                    {
                        upDir.Add("path", Directory.GetParent(pathToItem).FullName);  // Path to item
                    }
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
                    try { dir.Add("hasChildren", hasChildren(child)); }
                    catch { dir.Add("hasChildren", false); }  // If this directory has an unauthorized access exception
                    childrenContainer.Add(dir);
                }

                return childrenContainer;
            }
            // IF REAL FILE (e.g. .etl.zip, .etl)
            else if (new FileInfo(pathToItem).Exists)
            {
                // TODO: Separate this into another function, if not another endpoint
                // Assume only .etl.zip for now
                string etlFileName = pathToItem;
                var etlFile = PerfViewExtensibility.CommandEnvironment.OpenETLFile(etlFileName);
                TraceEvents events = etlFile.TraceLog.Events;

                // Create CPU Stacks as child
                Stacks CPUStacks = etlFile.CPUStacks();

                Dictionary<string, object> child = new Dictionary<string, object>();
                childrenContainer.Add(child);
                child.Add("text", "CPU Stacks");
                child.Add("type", "stacks");
                child.Add("stackType", "CPU");
                child.Add("path", pathToItem);
                child.Add("hasChildren", false);

                // TODO: Create Process / Files / Registry Stacks as child
                // TODO: Create TraceInfo htmlReport as child
                // TODO: Create Processes htmlReport as child
                // TODO: Create Events as child
                // TODO: Create Memory Group as child
                // TODO: Create Advanced Group as child

                return childrenContainer;
            }


            // TODO: Form proper response
            return null;
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

    }
}
