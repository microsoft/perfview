using System;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Privides a quick HTML users guide.  
/// </summary>
public class UsersGuide
{
    /// <summary>
    /// Displayes an the embeded HTML user's guide in a browser. 
    /// </summary>
    /// <returns>true if successful.</returns>
    public static bool DisplayUsersGuide(string resourceName)
    {
        string tempDir = Environment.GetEnvironmentVariable("TEMP");
        if (tempDir == null)
        {
            tempDir = ".";
        }

        string appPath = System.Reflection.Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName;
        string appName = Path.GetFileNameWithoutExtension(appPath);
        string usersGuideFilePath = Path.Combine(tempDir, appName + "." + resourceName);
        if (ResourceUtilities.UnpackResourceAsFile(@".\" + resourceName, usersGuideFilePath))
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(usersGuideFilePath);
            process.Start();
            // TODO I am leaking the file (but it is in the temp directory.  
            return true;
        }
        return false;
    }

    /// <summary>
    /// A trivial routine, but we want to share even trivial common code. 
    /// </summary>
    public static void DisplayConsoleAppUsersGuide(string resourceName)
    {
        if (DisplayUsersGuide(resourceName))
        {
            Console.WriteLine("Displaying Users guide.");
        }
        else
        {
            Console.WriteLine("Application does not have a user's guide.");
        }

        Environment.Exit(0);
    }
}
