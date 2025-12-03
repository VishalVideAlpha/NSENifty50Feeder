using CLIB.Models;
using CQGAPI.Helpers;
using CQGAPI.Models;
using Google.Cloud.Firestore;
using Google.Protobuf.Collections;
using MarketData2;
using Metadata2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UserSession2;
using WebAPI2;

namespace CQGAPI;

public class ApiBase
{
    private readonly ConcurrentDictionary<uint, string> Symbols = new();
    private readonly ConcurrentDictionary<string, ContractMetadata> ContractMetaDatas = new();
    private readonly ConcurrentBag<string> SubscribeSymbols = new();
    private readonly WebSocketServer _webSocket;
    private LogonResult? _logonResult;

    private readonly ILogger<ApiBase> _logger;
    private readonly ConfigInfo _configInfo;
    private readonly InstrumentListener _instrumentListener;

    private Subject<string>? dataStream;
    private int notificationCount;
    private bool dataResumed;

    public delegate Task OnRate(Rate rate);
    public event OnRate? OnRateUpdate;

    public delegate Task OnDataStateChanged(string message);
    public event OnDataStateChanged? DataStateChanged;
    public ApiBase(ILogger<ApiBase> logger, ConfigInfo config, InstrumentListener instrumentListener)
    {
        _logger = logger;
        _instrumentListener = instrumentListener;
        _configInfo = config ?? throw new ArgumentNullException(nameof(_configInfo));
        _webSocket = new WebSocketServer();
        IDisposable _disposable = Observable.Interval(TimeSpan.FromSeconds(20), System.Reactive.Concurrency.NewThreadScheduler.Default).Subscribe(async l => await SendPing());
    }
    static uint requestID = 111;
    public async Task Initialize()
    {
        try
        {
            //  requestID = GetNextId();
            _webSocket.OnClose += _webSocket_OnClose;

            _webSocket.On("Response", async data =>
            {
                ServerMsg serverMsg = ServerMsg.Parser.ParseFrom(data);
                if (serverMsg.LogonResult != null)
                {
                    _logonResult = serverMsg.LogonResult;
                    serverBaseTime = serverMsg.LogonResult.BaseTime;
                    _logger.LogInformation(serverMsg.LogonResult.ToString());
                    await SubscribeSymbol(_instrumentListener.dctInstruments.Values.Select(x => x.symbol).ToList());
                }
                else if (serverMsg.Pong != null)
                {
                    DateTime basetime = DateTime.Parse(serverBaseTime).ToUniversalTime();
                    DateTime serverDateTime = basetime.AddMilliseconds(serverMsg.Pong.PongUtcTime);
                    Console.WriteLine($"Pong Time: {serverDateTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
                }
                else if (serverMsg.MarketDataSubscriptionStatuses.Count > 0)
                {

                    if (serverMsg.RealTimeMarketData.Count > 0)
                    {
                        GetRealTimeMarketData(serverMsg.RealTimeMarketData);
                    }
                    foreach (var item in serverMsg.MarketDataSubscriptionStatuses)
                    {
                        if (item.StatusCode != 0)
                        {
                            _logger.LogInformation(item.ToString());
                        }

                    }
                }
                else if (serverMsg.InformationReports != null && serverMsg.InformationReports.Count > 0)
                {
                    foreach (var info in serverMsg.InformationReports)
                    {
                        if (info.StatusCode == 0 && info.SymbolResolutionReport != null)
                        {
                            var contractMetadata = info.SymbolResolutionReport.ContractMetadata;
                            if (contractMetadata == null) continue;
                            if (!string.IsNullOrEmpty(contractMetadata.Title) && !Symbols.ContainsKey(contractMetadata.ContractId))
                            {
                                ClientMsg clientSubscription = new();
                                if (!clientSubscription.MarketDataSubscriptions.Any(x => x.ContractId == contractMetadata.ContractId))
                                {
                                    requestID += 1;
                                    Symbols.TryAdd(contractMetadata.ContractId, contractMetadata.Title);
                                    ContractMetaDatas.TryAdd(contractMetadata.Title, contractMetadata);
                                    clientSubscription.MarketDataSubscriptions.Add(new MarketDataSubscription
                                    {
                                        ContractId = contractMetadata.ContractId,
                                        RequestId = requestID,
                                        Level = 4
                                    });
                                    _logger.LogInformation(clientSubscription.ToString());
                                    await _webSocket.SendAsync(clientSubscription);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation(info.ToString());
                        }
                    }

                }
                else if (serverMsg.RealTimeMarketData != null)
                {
                    GetRealTimeMarketData(serverMsg.RealTimeMarketData);
                }
                else if (serverMsg.UserMessages != null)
                {
                    _logger.LogInformation(serverMsg.UserMessages.ToString());
                }
                else if (serverMsg.RestoreOrJoinSessionResult != null)
                {
                    if (serverMsg.RestoreOrJoinSessionResult.ResultCode == 0)
                    {
                        _logger.LogInformation("Rejoin successfully");
                        _logger.LogInformation(serverMsg.RestoreOrJoinSessionResult.ToString());
                    }
                    else
                    {
                        _logger.LogInformation("Going for relogin");
                        await ServerStart();
                    }

                }
            });
            await _webSocket.Start(_configInfo.Url);
            _instrumentListener.OnTaskCompleted += _instrumentListener_OnTaskCompleted;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.StackTrace);
        }
    }

    private void GetRealTimeMarketData(RepeatedField<RealTimeMarketData> realTimeMarketData)
    {
        try
        {
            foreach (var item in realTimeMarketData)
            {
                if (Symbols.TryGetValue(item.ContractId, out string? symbol))
                {
                    Rate rate = new()
                    {
                        Symbol = symbol,
                        Bid = 0.475,
                        Ask = 0.475,
                        Close = 0.475,
                        High = 0.475,
                        Low = 0.475,
                        Ltp = 0.475,
                        Open = 0.475
                    };

                    if (item != null && item.Quotes.Count > 0)
                    {
                        var ohclData = item.MarketValues;
                        if (ohclData != null && ohclData.Count > 0)
                        {

                            foreach (var ohcl in ohclData)
                            {
                                rate.Close = rate.Close == 0.475 ? ohcl.ScaledYesterdaySettlement : rate.Close;
                                rate.Open = rate.Open == 0.475 ? ohcl.ScaledOpenPrice : rate.Open;
                                rate.High = rate.High == 0.475 ? ohcl.ScaledHighPrice : rate.High;
                                rate.Low = rate.Low == 0.475 ? ohcl.ScaledLowPrice : rate.Low;
                                //_logger.LogInformation(ohcl.ToString());
                                //Console.WriteLine($"{symbol}-Open:{item2.ScaledOpenPrice}-Low:{item2.ScaledLowPrice}-High:{item2.ScaledHighPrice}-Close:{item2.ScaledClosePrice}");
                            }
                        }

                        foreach (Quote quote in item.Quotes)
                        {


                            if (quote.Type == 0)
                                rate.Ltp = rate.Ltp == 0.475 ? quote.ScaledPrice : rate.Ltp;
                            else if (quote.Type == 1)
                                rate.Bid = rate.Bid == 0.475 ? quote.ScaledPrice : rate.Bid;
                            else if (quote.Type == 2)
                                rate.Ask = rate.Ask == 0.475 ? quote.ScaledPrice : rate.Ask;

                            if (quote.Indicators.Count > 0)
                            {
                                foreach (var indi in quote.Indicators)
                                {
                                    if (indi == 1)
                                    {
                                        rate.Open = quote.ScaledPrice;
                                        _logger.LogInformation($"{symbol} Open change {quote.ScaledPrice}");
                                    }
                                    else if (indi == 2)
                                    {
                                        rate.High = quote.ScaledPrice;
                                        _logger.LogInformation($"{symbol} High change {quote.ScaledPrice}");
                                    }
                                    else if (indi == 3)
                                    {
                                        rate.Low = quote.ScaledPrice;
                                        _logger.LogInformation($"{symbol} Low change {quote.ScaledPrice}");
                                    }
                                    //else if (indi == 11)
                                    //{
                                    //    rate.Close = quote.ScaledPrice;
                                    //    _logger.LogInformation($"Close change {quote.ScaledPrice}");
                                    //}

                                }
                            }

                        }


                    }
                    if (ContractMetaDatas.TryGetValue(rate.Symbol, out var metadata))
                    {
                        rate.ContractSymbol = metadata.ContractSymbol;
                    }
                    //Console.WriteLine(rate);
                    OnRateUpdate?.Invoke(rate);
                }
            }
            dataStream?.OnNext("Data received at " + DateTime.Now);
        }
        catch (Exception ex)
        {

            _logger.LogError(ex.Message, ex.StackTrace);
        }
    }

    private static uint counter = 0;
    public static uint GetNextId()
    {
        return (uint)(DateTime.UtcNow.Ticks & 0xFFFFFFFF); // Mask to fit in uint
    }
    //private static readonly string filePath = "unique_id.txt";
    //private static uint ReadLastValue()
    //{
    //    if (File.Exists(filePath))
    //    {
    //        string content = File.ReadAllText(filePath);
    //        if (uint.TryParse(content, out uint value))
    //            return value;
    //    }
    //    return 1; // Start from 1 if file doesn't exist
    //}

    private async void _webSocket_OnClose(string message)
    {
        _logger.LogInformation($"Connection Close reson :{message}");
        if (_logonResult != null)
        {
            ClientMsg clientMsg = new ClientMsg();
            clientMsg.RestoreOrJoinSession = new RestoreOrJoinSession
            {
                ClientAppId = _configInfo.ClientAppId,
                SessionToken = _logonResult.SessionToken,
                ProtocolVersionMajor = _configInfo.ProtocolVersionMajor,
                ProtocolVersionMinor = _configInfo.ProtocolVersionMinor,


            };
            await _webSocket.SendAsync(clientMsg);
        }
        else
        {
            await ServerStart();
        }

    }

    private async Task _instrumentListener_OnTaskCompleted()
    {
        await SubscribeSymbol(_instrumentListener.dctInstruments.Values.Select(x => x.symbol).ToList());
    }

    public async Task ServerStart()
    {
        try
        {
            ClientMsg clientMsg = new ClientMsg
            {
                Logon = new Logon
                {
                    UserName = _configInfo.Username,
                    Password = _configInfo.Password,
                    ClientAppId = _configInfo.ClientAppId,
                    ClientVersion = _configInfo.ClientVersion,
                    ProtocolVersionMinor = _configInfo.ProtocolVersionMinor,
                    ProtocolVersionMajor = _configInfo.ProtocolVersionMajor
                }
            };
            await _webSocket.SendAsync(clientMsg);
            if (dataStream == null)
            {
                dataStream = new Subject<string>();
                dataStream
            .Throttle(TimeSpan.FromSeconds(20))
            .Subscribe(_ =>
            {
                if (notificationCount < 3)
                {
                    DayOfWeek today = DateTime.Now.DayOfWeek;
                    if (today != DayOfWeek.Saturday && today != DayOfWeek.Sunday)
                    {
                        DataStateChanged?.Invoke($"{_configInfo.ProjID} No data has been received in the last 15 seconds. Notifying admin.");
                        notificationCount++;
                    }

                }
                dataResumed = false;
            });

                // Handle new data arrival
                dataStream.Subscribe(data =>
                {
                    if (!dataResumed) // Notify admin that data is being received again
                    {
                        DataStateChanged?.Invoke($"{_configInfo.ProjID} Data resumed.");
                        dataResumed = true;
                        notificationCount = 0; // Reset notification count
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.StackTrace);
        }
    }
    string serverBaseTime = string.Empty;
    private async Task SendPing()
    {
        try
        {
            if (!string.IsNullOrEmpty(serverBaseTime))
            {
                DateTime basetime = DateTime.Parse(serverBaseTime).ToUniversalTime();

                // Get current UTC time
                DateTime currentTime = DateTime.Now;

                // Calculate the offset in milliseconds
                long pingTimeOffset = (long)(currentTime - basetime).TotalMilliseconds;

                //DateTime serverDateTime = basetime.AddMilliseconds(pingTimeOffset);
                //Console.WriteLine($"Ping Time: {serverDateTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
                await _webSocket.SendAsync(new ClientMsg
                {
                    Ping = new Ping
                    {
                        PingUtcTime = pingTimeOffset,
                        Token = "Alpha"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.StackTrace);
        }
    }
    uint msgID = 1;
    public async Task SubscribeSymbol(List<string> symbols)
    {
        try
        {



            ClientMsg clientMsg1 = new ClientMsg();
            List<string> lstTPSymbol = _configInfo.WSTPSymbols.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (string symbol in symbols)
            {
                if (lstTPSymbol.Contains(symbol))
                {
                    continue;
                }
                if (!SubscribeSymbols.Contains(symbol))
                {
                    SubscribeSymbols.Add(symbol);
                    clientMsg1.InformationRequests.Add(new InformationRequest
                    {
                        Id = msgID,
                        SymbolResolutionRequest = new Metadata2.SymbolResolutionRequest { Symbol = symbol },
                    });
                    _logger.LogInformation($"{msgID} {symbol} going subscribe");
                    msgID++;
                }
            }

            //clientMsg1.InformationRequests.Add(new InformationRequest
            //{
            //    Id = msgID,
            //    SymbolResolutionRequest = new Metadata2.SymbolResolutionRequest { Symbol = "F.US.CLEH25" },
            //});
            if (clientMsg1.InformationRequests.Count > 0)
                await _webSocket.SendAsync(clientMsg1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.StackTrace);
        }
    }
}
