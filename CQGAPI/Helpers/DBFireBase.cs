using CLIB.Models;
using CQGAPI.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;

namespace CQGAPI.Helpers;

public class DBFireBase
{
    public FirestoreDb db = null;
    private static readonly object padlock = new object();
    private readonly ILogger<DBFireBase> _logger;
    private readonly ConfigInfo _configInfo;

    private readonly AESDecryptor _decryptor;


    // private readonly AppUtilities _utilities;

    public async Task<CollectionReference> GetCollectionReference(string collectionName)
    {
        return db.Collection(collectionName);
    }

    public DBFireBase(ILogger<DBFireBase> logger, ConfigInfo configInfo, AESDecryptor decryptor)
    {
        _logger = logger;
        _configInfo = configInfo;

        _decryptor = decryptor;

        // _utilities = utilities;

        string url = _configInfo.ServiceAccount;


        string encryptedJson = _decryptor.GetEncryptedServiceAccountAsync(url).Result;
        string decrypted = _decryptor.Decrypt(encryptedJson, GetKey(_configInfo.ProjectKey));
        ConnectAndReadFirestoreAsync(decrypted).Wait();

        //db = FirestoreDb.Create(_configInfo.ProjID);
    }

    public static string GetKey(string projId)
    {
        if (projId.CompareTo("dev") == 0)
        {
            return "1R60i2T4Rh7Egj49tGioPN37O5puL1p8";
        }
        else if (projId.CompareTo("prod") == 0)
        {
            return "dE40u0X16t6Bwe12aDtuYU1bK9tyUtn0";
        }
        return string.Empty;
    }

    private string GetProjectID(string decryptJson)
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

    public async Task ConnectAndReadFirestoreAsync(string decryptedJson)
    {
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(decryptedJson));

            var credential = GoogleCredential.FromStream(stream);
            var projID=GetProjectID(decryptedJson);
            _configInfo.ProjID=projID;

            FirestoreDbBuilder builder = new FirestoreDbBuilder
            {
                ProjectId = projID,
                Credential = credential
            };

            db = builder.Build();


            _logger.LogInformation("✅ Connected to Firestore. Collections:");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔥 Firestore connection failed");
        }
    }



}
