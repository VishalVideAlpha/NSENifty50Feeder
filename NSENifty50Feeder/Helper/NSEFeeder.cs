using CQGAPI;
using CQGAPI.Helpers;

using Microsoft.AspNetCore.SignalR.Client;
using NSEFeederService;
using NSENifty50Feeder.Data;
using NSENifty50Feeder.Helper;
using Serilog;

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;

namespace CQGFeederMatchTrader.Helper;

public delegate void NseData(Rate rate);
public class NSEFeeder
{
    private readonly ILogger<NSEFeeder> _logger;
    private readonly string _url;
    private readonly string _securityFile;
    private string _contractFile;
    private readonly double _divisior;
    private HubConnection? _hubConnection;
    public event NseData? DataReceived;

    private Dictionary<int, string> lstEqContract = new Dictionary<int, string>();
    private Dictionary<string, int> lstNFOContract = new Dictionary<string, int>();
    private static readonly string DateFormat = "dd/MM/yyyy";
    private DateTime marketStartTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 09, 00, 00);
    private DateTime marketStopTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 15, 30, 00);
    private readonly InstrumentListener _listener;
    public NSEFeeder(ILogger<NSEFeeder> logger, IConfiguration configuration, FileService fileService, InstrumentListener listener)
    {
        _listener = listener;
        _fileService = fileService;
        _logger = logger;
        //  _contractFile = configuration["ContractFile"] ?? throw new ArgumentNullException(nameof(_contractFile));
        //   _securityFile = configuration["SecurityFile"] ?? throw new ArgumentNullException(nameof(_securityFile));
        // if (!File.Exists(_securityFile)) throw new ArgumentNullException(nameof(_securityFile));
        _url = configuration["NUrl"] ?? throw new ArgumentNullException(nameof(_url));
        string divisior = configuration["NSEDivisior"] ?? throw new ArgumentNullException(nameof(_divisior));
        _divisior = Convert.ToDouble(divisior);
        Observable.Interval(TimeSpan.FromMinutes(1), NewThreadScheduler.Default).Subscribe(async l => await CheckConnection());
    }

    private async Task CheckConnection()
    {
        try
        {
            if (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
            {
                var indiaTime = DateTime.UtcNow.AddHours(5.5);
                if (marketStartTime.TimeOfDay < indiaTime.TimeOfDay && indiaTime.TimeOfDay < marketStopTime.TimeOfDay)
                {
                    //if (await ContractInit())
                    //{
                    //    _logger.LogInformation("connnected with *** market");
                    //}
                }

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, "Fail on check N** connection");
        }
    }

    public async Task<bool> Initailize()
    {
        try
        {
           var filePath = Path.Combine($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}", "FTP", "contract.txt");
            await ContractInit(filePath);
            //InitEqContract(_securityFile);
            InitNFOContracts(filePath);

            //_hubConnection = new HubConnectionBuilder()
            //                .WithUrl($"{_url}/foDataDemo")
            //                .WithAutomaticReconnect() // Enables automatic reconnection
            //                .Build();
            //_hubConnection.On<byte[]>("demo", (message) =>

            _hubConnection = new HubConnectionBuilder()
                            .WithUrl($"{_url}/foData")
                            .WithAutomaticReconnect() // Enables automatic reconnection
                            .Build();
            _hubConnection.On<byte[]>("live", (message) =>

            {
                MBPData mBPData = MBPData.Parser.ParseFrom(message);

                if (mBPData != null)
                {
                    var key = string.Empty;
                    if (dctTokenSymbol.ContainsKey(mBPData.Token))
                    {
                        key = dctTokenSymbol[mBPData.Token];
                    }
                    var rate = new Rate
                    {
                        Ask = mBPData.Ask,
                        Bid = mBPData.Bid,
                        ContractSymbol = key,
                        Symbol = key,
                        Close = mBPData.Close,
                        Low = mBPData.Low,
                        High = mBPData.High,
                        Ltp = mBPData.Ltp,
                        Open = mBPData.Open,
                        TimeStamp = DateTime.UtcNow.ToString("M/d/yyyy hh:mm:ss tt"),
                        SourceAsk = mBPData.Ask,
                        SourceBid = mBPData.Bid,
                        IsDemo = mBPData.IsDemo

                    };

                    var lstInstrument = _listener.dctInstruments.Where(x => x.Value.symbol.ToLower() == rate.Symbol.ToLower()).ToDictionary(x => x.Key, x => x.Value);
                    if (lstInstrument != null)
                    {
                        foreach (var symbolId in lstInstrument.Keys)
                        {
                            rate.Symbol = symbolId;
                            DataReceived?.Invoke(rate);
                        }

                    }
                }
            });

            _hubConnection.Reconnected += CmReconnected;
            await _hubConnection.StartAsync();
            await SubscribeSymbols();
            _listener.OnNewSymbolRecieved += _listener_OnNewSymbolRecieved; ;
            _logger.LogInformation("NSE Connected successfully");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError("Request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex}");
        }
        return false;
    }

    private async Task _listener_OnNewSymbolRecieved()
    {
        await SubscribeSymbols();
    }

    Dictionary<int, string> dctTokenSymbol = new Dictionary<int, string>();
    private async Task SubscribeSymbols()
    {
        //var selectedTokens = lstNFOContract.Values.Where(x => InstrumentListener.SymbolsWithExpiry.Any(y => x.Symbol == y.Key && x.Expiry == y.Value)).Select(x => x.Token).ToList();
        //if (InstrumentListener.SymbolsWithExpiry.ContainsKey(symbol) && InstrumentListener.SymbolsWithExpiry[symbol] == expiry)
        //{
        //    lstNFOContract.Add(tokenNo, contract);
        //}
        List<int> lstToken = new List<int>();
        foreach (var key in InstrumentListener.SymbolsWithExpiry.Keys)
        {
            if (lstNFOContract.ContainsKey(key))
            {
                var token = lstNFOContract[key];
                if (!dctTokenSymbol.ContainsKey(token))
                {
                    dctTokenSymbol.Add(token, key);
                }
                lstToken.Add(token);
            }
        }

        await _hubConnection.InvokeAsync("Subscribe", lstToken);
        InstrumentListener.SymbolsWithExpiry.Clear();


    }

    //public async Task<bool> Initailize()
    //{
    //    try
    //    {
    //       await ContractInit();
    //        //InitEqContract(_securityFile);
    //        InitNFOContracts(_contractFile);
    //        _hubConnection = new HubConnectionBuilder()
    //                        .WithUrl($"{_url}/eqdata")
    //                        .WithAutomaticReconnect() // Enables automatic reconnection
    //                        .Build();
    //        _hubConnection.On<byte[]>("live", (message) =>
    //        {
    //            MBPData mBPData = MBPData.Parser.ParseFrom(message);

    //            if (mBPData != null)
    //            {

    //                //double ask = Math.Ceiling((mBPData.Ask / _divisior) * 100) / 100;
    //                //double bid = Math.Floor((mBPData.Bid / _divisior) * 100) / 100;
    //                DataReceived?.Invoke(new Rate
    //                {
    //                    Ask = mBPData.Ask,
    //                    Bid = mBPData.Bid,
    //                    ContractSymbol = mBPData.Contract.Symbol,
    //                    Symbol = mBPData.Contract.Symbol,
    //                    Close = mBPData.Close,
    //                    Low = mBPData.Low,
    //                    High = mBPData.High,
    //                    Ltp = mBPData.Ltp,
    //                    Open = mBPData.Open,
    //                    TimeStamp = DateTime.UtcNow.ToString("M/d/yyyy hh:mm:ss tt"),
    //                    SourceAsk = mBPData.Ask,
    //                    SourceBid = mBPData.Bid,
    //                });
    //            }
    //        });
    //        _hubConnection.Reconnected += CmReconnected;
    //        await _hubConnection.StartAsync();

    //        await _hubConnection.InvokeAsync("Subscribe", lstEqContract.Keys.ToList());
    //        _logger.LogInformation("NSE Connected successfully");
    //        return true;
    //    }
    //    catch (HttpRequestException ex)
    //    {
    //        _logger.LogError($"Network error: {ex.Message}");
    //    }
    //    catch (TaskCanceledException ex)
    //    {
    //        _logger.LogError("Request timed out.");
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError($"Unexpected error: {ex}");
    //    }
    //    return false;
    //}

    private async Task CmReconnected(string? arg)
    {
        if (_hubConnection != null)
            await _hubConnection.InvokeAsync("Subscribe", lstNFOContract.Keys.ToList());
    }
    private readonly FileService _fileService;
    private async Task<bool> ContractInit(string filePath)
    {
        try
        {
            var data = await _fileService.DownloadAsync(FileType.contract);
            //string filePath = string.Empty;
            if (data != null && string.IsNullOrEmpty(data.FileName))
            {
                _logger.LogInformation("Contract file download failed. Please check the logs for more details.");
                await _fileService.DownloadAsync(FileType.contract);
                return false;
            }
            if (data == null)
            {
                filePath = Path.Combine($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)},contract.txt");
                _contractFile = filePath;
            }
            filePath = data.FileName;
            if (data != null && data.FileName != null)
            {

                if (data != null)
                    _contractFile = data.FileName;
                if (string.IsNullOrEmpty(filePath))
                    throw new Exception("Contract file path is not configured in App.config.");

            }
            _logger.LogInformation("Contracts initialized successfully from {FilePath}", filePath);
            return true;


        }
        catch (Exception ex)
        {
            MethodBase currentMethod = MethodBase.GetCurrentMethod();
            _logger.LogError(ex,
                "Error in {MethodName}: {ErrorMessage}",
                currentMethod?.Name,
                ex.Message);
            return false;
        }



    }
    public static DateTime ConvertDatetimeFromSeconds(long timeStamp)
    {
        return new DateTime(1980, 1, 1).AddSeconds(timeStamp);
    }
    public void InitNFOContracts(string filePath)
    {
        lstNFOContract = new Dictionary<string, int>();
        string expiry = string.Empty;
        DateTime dtExpiry = DateTime.MinValue;

        using var reader = new StreamReader(filePath);
        int tokenNo = 0;

        try
        {

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split('|');

                if (int.TryParse(values[0], out tokenNo))
                {
                    if (!string.IsNullOrEmpty(values[2]) &&
                        (values[2].Contains("FUT")))
                    {
                        dtExpiry = ConvertDatetimeFromSeconds(long.Parse(values[6]));
                        expiry = dtExpiry.ToString(DateFormat).ToUpper();
                        var symbol = values[3];
                        var contract = new Contract()
                        {
                            Token = tokenNo,
                            Symbol = symbol,
                            Expiry = expiry,
                            Strike = Convert.ToDouble(values[7]) / 100,
                            OptionType = values[8],
                        };
                        var key = $"{symbol}:{expiry}";
                        lstNFOContract.Add(key, contract.Token);

                    }
                }

                else
                {
                    Log.Warning("No NFO contracts were found in file: {FilePath}", filePath);
                }
            }
            //  var monthlyExpiries = GetMonthlyExpiries(lstNFOContract);
            //  var monthlyExpiries = GetNextMonthExpiries(lstNFOContract);

            //lstNFOContract.Clear();

            //foreach (var contract in lstNFOContract.Values)
            //{
            //    lstNFOContract[contract.Token] = contract;
            //}




        }
        catch (Exception ex)
        {
            MethodBase currentMethod = MethodBase.GetCurrentMethod();
            _logger.LogError(ex, "Error in {Method} while processing file: {FilePath}", currentMethod?.Name, filePath);
        }
    }
    //private Dictionary<string, Contract> GetMonthlyExpiries(Dictionary<int, Contract> contracts)
    //{
    //    return contracts.Values
    //        .Select(r =>
    //        {
    //            if (DateTime.TryParseExact(r.Expiry, "ddMMMyyyy",
    //                System.Globalization.CultureInfo.InvariantCulture,
    //                System.Globalization.DateTimeStyles.None, out var dt))
    //            {
    //                return new { Contract = r, r.Symbol, Date = dt };
    //            }
    //            return null;
    //        })
    //        .Where(x => x != null
    //                    && x.Date.Year == DateTime.Today.Year
    //                    && x.Date.Month == DateTime.Today.Month)
    //        .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
    //        .ToDictionary(
    //            g => g.Key,
    //            g => g.OrderByDescending(x => x.Date).First().Contract,
    //            StringComparer.OrdinalIgnoreCase
    //        );
    //}
    private Dictionary<string, Contract> GetNextMonthExpiries(Dictionary<int, Contract> contracts)
    {
        return contracts.Values
            .Select(r =>
            {
                if (DateTime.TryParseExact(r.Expiry, "ddMMMyyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                {
                    return new { Contract = r, r.Symbol, Date = dt };
                }
                return null;
            })
            .Where(x => x != null
                        && x.Date.Year == DateTime.Today.AddMonths(1).Year
                        && x.Date.Month == DateTime.Today.AddMonths(1).Month)
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Date).First().Contract,
                StringComparer.OrdinalIgnoreCase
            );
    }



    public void InitEqContract(string path)
    {

        try
        {
            if (File.Exists(path))
            {
                using (StreamReader reader = new StreamReader(path))
                {

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            var values = line.Split('|');
                            if (int.TryParse(values[0], out int tokenNo))
                            {
                                if (values[1] != string.Empty && values[2].Contains("EQ") && values[7] == "6")
                                {

                                    if (nifty50Stocks.Contains(values[1]))
                                        lstEqContract.Add(int.Parse(values[0]), values[1]);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // AppLogWriter.WriteInLog(ex, MethodBase.GetCurrentMethod());
        }

    }

    List<string> nifty50Stocks = new List<string>
{
    "ADANIPORTS",
    "ASIANPAINT",
    "AXISBANK",
    "BAJAJ-AUTO",
    "BAJFINANCE",
    "BAJAJFINSV",
    "BPCL",
    "BHARTIARTL",
    "BRITANNIA",
    "CIPLA",
    "COALINDIA",
    "DIVISLAB",
    "DRREDDY",
    "EICHERMOT",
    "GRASIM",
    "HCLTECH",
    "HDFCBANK",
    "HDFCLIFE",
    "HEROMOTOCO",
    "HINDALCO",
    "HINDUNILVR",
    "ICICIBANK",
    "ITC",
    "INFY",
    "JSWSTEEL",
    "KOTAKBANK",
    "LT",
    "M&M",
    "MARUTI",
    "NTPC",
    "NESTLEIND",
    "ONGC",
    "POWERGRID",
    "RELIANCE",
    "SBILIFE",
    "SBIN",
    "SUNPHARMA",
    "TCS",
    "TATACONSUM",
    "TATAMOTORS",
    "TATASTEEL",
    "TECHM",
    "TITAN",
    "UPL",
    "ULTRACEMCO",
    "WIPRO",
    "ADANIENT",
    "HDFCAMC",
    "LTIM",
    "LTTS"
};


}
