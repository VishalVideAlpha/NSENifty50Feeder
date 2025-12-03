using CLIB.Models;
using CQGAPI;
using CQGAPI.Helpers;
using CQGAPI.Models;
using CQGFeederMatchTrader.Data;
using CQGFeederMatchTrader.Helper;
using Google.Protobuf;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace CQGFeederMatchTrader;

public class DataBroadCaster
{
    private readonly ILogger<DataBroadCaster> _logger;
    private readonly InstrumentListener _listener;
 
    private readonly ConfigInfo _configInfo;
    private readonly IServiceProvider _serviceProvider;

    private readonly UdpClient client = new UdpClient();
    private IPAddress? broadcastAddress;
    
    private readonly NSEFeeder _nSEFeeder;

    ConcurrentDictionary<string, Rate> dctRates = new ConcurrentDictionary<string, Rate>();
    public DataBroadCaster(ILogger<DataBroadCaster> logger, InstrumentListener listener,
         ConfigInfo options, IServiceProvider serviceProvider, NSEFeeder nSEFeeder)
    {
        _logger = logger;
        _listener = listener;
    
        _configInfo = options;
        _serviceProvider = serviceProvider;
        broadcastAddress = IPAddress.Parse(_configInfo.MIP);
     
        _nSEFeeder = nSEFeeder;
    
    }
    DateTime latestFeed = DateTime.MinValue;
    public async Task Initialize()
    {
        if (broadcastAddress == null) throw new ArgumentNullException(nameof(broadcastAddress));
        _nSEFeeder.DataReceived += _nSEFeeder_DataReceived;
        client.JoinMulticastGroup(broadcastAddress);
        client.EnableBroadcast = true;

        await _nSEFeeder.Initailize();
       
    }
  
    private async void _nSEFeeder_DataReceived(Rate rate)
    {
        //if(rate.Symbol=="ICICIBANK")
        //{

        //    var timeDiff = (System.DateTime.UtcNow - latestFeed).TotalSeconds;
        //    Console.WriteLine($"Bide {rate.Bid} Ask {rate.Ask} last updated after {timeDiff}");
        //    latestFeed = DateTime.UtcNow;
        //}
        var dataByte = rate.ToByteArray();
        await client.SendAsync(dataByte, dataByte.Length, new IPEndPoint(broadcastAddress, _configInfo.MPort));
    }

    public async Task OnRateUpdate(Rate rate)
    {
        try
        {

            if (broadcastAddress != null && rate != null)
            {
                if (rate != null)
                {
                    var instruments = _listener.dctInstruments.Where(x => x.Value.symbol == rate.Symbol).FirstOrDefault().Value;
                    if (instruments != null)
                    {
                        rate.Ltp = rate.Ltp == 0.475 ? rate.Ltp : rate.Ltp / instruments.divider;
                        rate.Bid = rate.Bid == 0.475 ? rate.Bid : rate.Bid / instruments.divider;
                        rate.Ask = rate.Ask == 0.475 ? rate.Ask : rate.Ask / instruments.divider;
                        rate.Open = rate.Open == 0.475 ? rate.Open : rate.Open / instruments.divider;
                        rate.Low = rate.Low == 0.475 ? rate.Low : rate.Low / instruments.divider;
                        rate.High = rate.High == 0.475 ? rate.High : rate.High / instruments.divider;
                        rate.Close = rate.Close == 0.475 ? rate.Close : rate.Close / instruments.divider;

                        if (!dctRates.ContainsKey(rate.Symbol))
                        {
                            dctRates.TryAdd(rate.Symbol, new Rate { Symbol = rate.Symbol, ContractSymbol = rate.ContractSymbol });
                        }

                        var data = dctRates[rate.Symbol];

                        data.Bid = GetBid(data, rate, instruments);
                        data.Ask = GetAsk(data, rate, instruments);
                        data.Ltp = rate.Ltp == 0.475 ? data.Ltp : rate.Ltp;
                        data.Open = rate.Open == 0.475 ? data.Open : rate.Open;
                        data.High = rate.High == 0.475 ? data.High : rate.High;
                        data.Low = rate.Low == 0.475 ? data.Low : rate.Low;
                        data.Close = rate.Close == 0.475 ? data.Close : rate.Close;

                        // data.High = data.High < data.Ltp ? data.Ltp : data.High;
                        //data.Low = data.Low > data.Ltp ? data.Ltp : data.Low;

                        data.TimeStamp = DateTime.UtcNow.ToString();
                        //Console.WriteLine(data.ToString());
                        //       await UpdateDataBase(data);
                        var dataByte = data.ToByteArray();
                        await client.SendAsync(dataByte, dataByte.Length, new IPEndPoint(broadcastAddress, _configInfo.MPort));

                    }
                }

            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.StackTrace);
        }
    }


    private async Task UpdateDataBase(Rate data)
    {
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var appDBContext = scope.ServiceProvider.GetService<AppDBContext>();
                if (appDBContext != null)
                {
                    string Key = $"{data.Symbol}_{DateTime.Now:ddMMMyy}";
                    var oldData = appDBContext.CurrentRates.FirstOrDefault(x => x.Key == Key);
                    if (oldData != null)
                    {
                        oldData.Open = data.Open;
                        oldData.High = data.High;
                        oldData.Low = data.Low;
                        oldData.Close = data.Close;
                        oldData.Bid = data.Bid;
                        oldData.Ask = data.Ask;
                        oldData.Ltp = data.Ltp;
                    }
                    else
                    {
                        appDBContext.CurrentRates.Add(new DBRate
                        {
                            Key = Key,
                            Symbol = data.Symbol,
                            Ask = data.Ask,
                            Bid = data.Bid,
                            Close = data.Close,
                            High = data.High,
                            Low = data.Low,
                            Ltp = data.Ltp,
                            Open = data.Open
                        });
                    }
                    await appDBContext.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.StackTrace);
        }
    }

    private double GetAsk(Rate data, Rate rate, Instruments instruments)
    {
        var sym = instruments.symbol;
        if (sym == "NGEM25")
        {

        }
        if (rate.Ask == 0.475) return CeilToTickSize(data.Ask, instruments.tickSize);

        if (data.Bid == 0 && data.Ask != 0)
        {
            data.Bid = rate.Ask - instruments.tickSize;
        }
        else if (data.Bid == rate.Ask || data.Bid > rate.Ask)
        {
            data.Bid = rate.Ask - instruments.tickSize;
        }


        return CeilToTickSize(rate.Ask, instruments.tickSize);
    }

    public static double FloorToTickSize(double price, double tickSize)
    {
        if (tickSize <= 0)
        {
            return price;
        }
        return Math.Floor(price / tickSize) * tickSize;
    }

    public static double CeilToTickSize(double price, double tickSize)
    {
        if (tickSize <= 0)
        {
            return price;
        }
        return Math.Ceiling(price / tickSize) * tickSize;
    }

    private double GetBid(Rate data, Rate rate, Instruments instruments)
    {
        var sym = instruments.symbol;
        if (sym == "NGEM25")
        {

        }
        if (rate.Bid == 0.475) return FloorToTickSize(data.Bid, instruments.tickSize);

        if (data.Ask == 0 && data.Bid != 0)
        {
            data.Ask = rate.Bid + instruments.tickSize;
        }
        else if (data.Ask == rate.Bid || rate.Bid > data.Ask)
        {
            data.Ask = rate.Bid + instruments.tickSize;
        }


        return FloorToTickSize(rate.Bid, instruments.tickSize);
    }
}
