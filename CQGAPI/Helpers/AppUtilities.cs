using Serilog;
using System.Text.RegularExpressions;

namespace CQGAPI.Helpers;

public class AppUtilities
{



    public static string GetProjectID(string decryptJson)
    {
        string pattern = "\"project_id\"\\s*:\\s*\"([^\"]+)\"";
        Match match = Regex.Match(decryptJson, pattern);

        if (match.Success)
        {
            string projectId = match.Groups[1].Value;
            Console.WriteLine("Project ID: " + projectId);  // Output: midas-trading
            return projectId;
        }
        Console.WriteLine("Project ID not found");  // Output: midas-trading
        return string.Empty;
    }
    //public static Task SetEnvironmentVar(string envPath)
    //{

    //    Log.Information($"ENV PATH {envPath}");
    //    if (System.Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") != null && string.Compare(System.Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"), envPath) != 0)
    //    {
    //        System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", envPath);
    //    }
    //    if (System.Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") == null)
    //    {
    //        System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", envPath);
    //    }
    //    return Task.CompletedTask;
    //}
}
