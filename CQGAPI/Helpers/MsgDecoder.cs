using System.Collections.Concurrent;
using WebAPI2;

namespace CQGAPI.Helpers;

public class MsgDecoder<T> where T : new()
{
    private ConcurrentDictionary<string, Action<byte[]>> dctAction = new ConcurrentDictionary<string, Action<byte[]>>();
    public Action<T>? DataAction { get; internal set; }

    public void Decode(byte[] data)
    {
        ServerMsg msg = ServerMsg.Parser.ParseFrom(data);
        if (msg == null || DataAction == null) return;


        if (msg.LogonResult is T logonResult)
        {
            DataAction.Invoke(logonResult);
        }
        if (msg.InformationReports is T informationReports)
        {
            DataAction.Invoke(informationReports);
        }
        if (msg.RealTimeMarketData is T realTimeMarketData)
        {
            DataAction.Invoke(realTimeMarketData);
        }
    }
}
public class EventName
{
    public const string OnLogon = "OnLogon";
    public const string Information = "Information";
    public const string RealTimeMarketData = "RealTimeMarketData";
}