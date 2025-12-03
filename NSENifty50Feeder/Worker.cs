using CLIB.Models;
using CQGAPI;
using CQGAPI.Helpers;
using CQGAPI.Models;

using Microsoft.Extensions.Options;
using System.Net;

namespace CQGFeederMatchTrader
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private readonly InstrumentListener _instrumentListener;
        private readonly ConfigInfo _configInfo;
        private readonly DataBroadCaster _broadCaster;
        private bool Initialize = false;


        public Worker(ILogger<Worker> logger,
            InstrumentListener instrumentListener, IOptions<ConfigInfo> options,
            DataBroadCaster dataBroadCaster)
        {

            _logger = logger;

            _instrumentListener = instrumentListener;
            _broadCaster = dataBroadCaster;
            _configInfo = options.Value ?? throw new ArgumentNullException(nameof(ConfigInfo));

        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // _instrumentListener.OnNewSymbolRecieved += OnTaskCompleted;
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                await _instrumentListener.ListenToFirestore("instruments", tcs);
                await tcs.Task;
                await InitBroadcaster();
                _logger.LogInformation("Service Started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
            }
        }
        private async Task InitBroadcaster()
        {
            try
            {
                if (!Initialize)
                {
                    await _broadCaster.Initialize();


                    Initialize = true;
                }
                //var mSymbol = _instrumentListener.dctInstruments.Values.Where(x => x.tag.Equals("US EQUITY", StringComparison.InvariantCultureIgnoreCase)).Select(x => x.symbol).ToList();
                //  var mSymbol= _instrumentListener.dctInstruments.Values.Where(x => !x.tag.Equals("EQUITY", StringComparison.InvariantCultureIgnoreCase)).Select(x=>x.symbol).ToList();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
            }
        }

    }
}
