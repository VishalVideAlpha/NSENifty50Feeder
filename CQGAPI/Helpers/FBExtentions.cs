using Serilog;
using System.Reflection;

namespace CQGAPI.Helpers;

public static class FBExtentions
{
    public static void RemoveTimeStampFromValue(this Dictionary<string, object> dctChange)
    {
        if (dctChange.ContainsKey("statusUpdatedAt"))
            dctChange["statusUpdatedAt"] = dctChange.ParseTimestampString("statusUpdatedAt");
        if (dctChange.ContainsKey("orderRecievedOnServerAt"))
            dctChange["orderRecievedOnServerAt"] = dctChange.ParseTimestampString("orderRecievedOnServerAt");
        if (dctChange.ContainsKey("timeStamp"))
            dctChange["timeStamp"] = dctChange.ParseTimestampString("timeStamp");
        if (dctChange.ContainsKey("updatedAt"))
            dctChange["updatedAt"] = dctChange.ParseTimestampString("updatedAt"); 
    }
    public static T GetFixValue<T>(this Dictionary<string, object> dctChange, string key)
    {
        if (dctChange.ContainsKey(key))
        {
            object value = dctChange[key];
            if (value is T)
            {
                return (T)value;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }
        return default(T);
    }
    public static DateTime ParseTimestampString(this Dictionary<string, object> dctChange, string key)
    {
        DateTime date = DateTime.MinValue;
        try
        {
            if (dctChange.ContainsKey(key))
            {
                string? utcDate = dctChange[key].ToString()!.Contains("Timestamp: ") ? dctChange[key].ToString()!.Split(new string[] { "Timestamp:" }, StringSplitOptions.RemoveEmptyEntries)[0] : dctChange[key].ToString();
                if (!string.IsNullOrEmpty(utcDate) && DateTime.TryParseExact(utcDate.Replace(" ", ""), "yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out date))
                {
                    return date;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.StackTrace}");
            throw;
        }

        return date;
    }
}
