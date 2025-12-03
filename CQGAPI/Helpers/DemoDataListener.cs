using CQGAPI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static CQGAPI.ApiBase;

namespace CQGAPI.Helpers
{
    public class DemoDataListener
    {

        InstrumentListener _listener;
        public event OnRate? OnRateUpdate;
        private readonly SqlHelper _sqlHelper;
        public DemoDataListener(InstrumentListener listener, SqlHelper sqlHelper)
        {
            _listener = listener;
            _sqlHelper = sqlHelper;

        }
        public static ConcurrentDictionary<string, Rate> dctRates = new ConcurrentDictionary<string, Rate>();
        public async Task StratListen()
        {
            try
            {
                await _sqlHelper.GetAllSymbolClose(_listener.dctInstruments);
                Random random = new Random();
                random.Next(1, 10);
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var newrate = new List<Rate>();
                            foreach (var data in dctRates.Values)
                            {
                                var instr = _listener.dctInstruments.Where(x => x.Value.symbol == data.ContractSymbol).FirstOrDefault().Value;
                                if (instr != null)
                                {
                                    int tickMove = random.Next(-10, 11); // Simulate ±10 ticks
                                    double tickSize = instr.tickSize;              // Based on instrument
                                    double bidPrice = Math.Round(data.Bid + tickMove * tickSize, 2) * instr.divider; // or more decimals
                                    double askPrice = Math.Round(data.Ask + tickMove * tickSize, 2) * instr.divider;

                                    Rate rate = new Rate
                                    {
                                        Ask = askPrice,
                                        Bid = bidPrice,
                                        Ltp = Math.Round(data.Ask + (random.NextDouble() / 100), 5) * instr.divider,
                                        Close = data.Close * instr.divider,
                                        ContractSymbol = $"F.US.{data.Symbol}",
                                        High = data.High * instr.divider,
                                        Low = data.Low * instr.divider,
                                        Open = data.Open * instr.divider,
                                        Symbol = data.Symbol,
                                        TimeStamp = data.TimeStamp
                                    };
                                    newrate.Add(rate);
                                }

                            }
                            foreach (var rate in newrate)
                            {
                                OnRateUpdate(rate);
                            }

                            await Task.Delay(1000);

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error: " + e.ToString());
                        }
                    }
                });


            }
            catch (Exception ex)
            {

            }
        }
    }
}
