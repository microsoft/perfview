/*  Copyright (c) Microsoft Corporation.  All rights reserved. */
/* AUTHOR: Vance Morrison   
 * Date  : 10/20/2007  */
using System;
using System.Diagnostics;
using System.IO;

namespace PerfView
{
    /// <summary>
    /// Privides a quick HTML users guide.  
    /// </summary>
    public static class UsersGuide
    {
        /// <summary>
        /// Displayes an the embeded HTML user's guide in a browser. 
        /// </summary>
        /// <returns>true if successful.</returns>
        public static bool DisplayUsersGuide(string resourceName, string anchor = "")
        {
            string tempDir = Environment.GetEnvironmentVariable("TEMP");
            if (tempDir == null)
            {
                return false;
            }

            string appPath = System.Reflection.Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName;
            string appName = Path.GetFileNameWithoutExtension(appPath);
            string appDir = Path.Combine(tempDir, appName);
            Directory.CreateDirectory(appDir);
            string usersGuideFilePath = Path.Combine(appDir, resourceName);
            if (ResourceUtilities.UnpackResourceAsFile(@".\" + resourceName, usersGuideFilePath))
            {
                string uri = usersGuideFilePath;
                {
                    if (!string.IsNullOrEmpty(anchor))
                    {
                        uri = "file:///" + uri.Replace('\\', '/').Replace(" ", "%20") + "#" + anchor;
                    }
                }
                Process.Start(uri);
                // process.StartInfo = new ProcessStartInfo("iexplore", uri);
                // process.StartInfo.Verb = "open";
                // process.Start();
                // TODO I am leaking the file (but it is in the children directory) 
                return true;
            }
            return false;
        }
    }
}