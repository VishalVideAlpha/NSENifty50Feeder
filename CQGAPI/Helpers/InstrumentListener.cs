using CQGAPI.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using SymbolBrowsing2;
using System.Collections.Concurrent;

namespace CQGAPI.Helpers;

public class InstrumentListener
{
    public delegate Task NewSymbol();
    public event NewSymbol? OnNewSymbolRecieved;
    private readonly ILogger<InstrumentListener> _logger;
    private readonly DBFireBase _dbFireBase;
    public static ConcurrentDictionary<string, string> SymbolsWithExpiry = new ConcurrentDictionary<string, string>();

    public ConcurrentDictionary<string, Instruments> dctInstruments = new();
    public InstrumentListener(ILogger<InstrumentListener> logger, DBFireBase dbFireBase)
    {
        _logger = logger;
        _dbFireBase = dbFireBase;
    }

    public async Task ListenToFirestore(string collectionName, TaskCompletionSource<bool> tcs)
    {
        try
        {
            CollectionReference collectionRef = await _dbFireBase.GetCollectionReference(collectionName);
            //Query query = collectionRef.WhereEqualTo("appName", "applicationName");
            Google.Cloud.Firestore.Query query = collectionRef.WhereEqualTo("deleted", false);

            FirestoreChangeListener listner = query.Listen(async snapshot =>
            {

                foreach (Google.Cloud.Firestore.DocumentChange change in snapshot.Changes)
                {

                    string expiryDate = string.Empty;
                    if (change.Document == null) continue;
                    var dctChange = change.Document.ToDictionary();
                    if (dctChange == null || dctChange.Count == 0) continue;
                    if (dctChange.ContainsKey("tag") && dctChange["tag"].ToString().ToUpper() == "EQUITY")
                    {
                        dctChange.RemoveTimeStampFromValue();
                        string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(dctChange);
                        if (string.IsNullOrEmpty(jsonString)) continue;
                        Instruments? data = Newtonsoft.Json.JsonConvert.DeserializeObject<Instruments>(jsonString);
                        data.symbolId = change.Document.Id;
                        if (data == null) continue;
                       
                        string symbol = dctChange["symbol"].ToString();
                        if (dctChange.ContainsKey("expiryDate"))
                        {
                            expiryDate = dctChange["expiryDate"].ToString();
                        }
                        //if (symbol == "ICICIBANK")
                        //{

                        //}
                        if (symbol != null && expiryDate != null)
                        {
                            string key = $"{symbol}:{expiryDate}";
                            if (!SymbolsWithExpiry.ContainsKey(key))
                            {
                                SymbolsWithExpiry.TryAdd(key, key);
                            }
                            data.symbol = key;
                            dctInstruments.AddOrUpdate(data.symbolId, data, (k, v) => data);
                        }
                    }


                }
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult(true);
                }
                else
                {
                    OnNewSymbolRecieved?.Invoke();
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.StackTrace);
        }

        return;
    }
}
