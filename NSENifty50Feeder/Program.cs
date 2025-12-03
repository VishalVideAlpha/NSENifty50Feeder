using CLIB.Models;
using CQGAPI;
using CQGAPI.Helpers;
using CQGAPI.Models;
using CQGFeederMatchTrader;
using CQGFeederMatchTrader.Data;
using CQGFeederMatchTrader.Helper;
using Google.Api;
using Microsoft.EntityFrameworkCore;
using NSENifty50Feeder.Helper;
using ProtoBuf.Meta;

var builder = Host.CreateApplicationBuilder(args);
string logFilePath=builder.Configuration.GetValue<string>("LogFileDir")??throw new ArgumentNullException(nameof(logFilePath));
if(!Directory.Exists(logFilePath)) Directory.CreateDirectory(logFilePath);
var logger = builder.AddSerilogServices(logFilePath);
string fileUrl = builder.Configuration["FileUrl"]
                                        ?? throw new ArgumentNullException("FileUrl");



try
{
    ConfigInfo config = builder.Configuration.GetSection(nameof(ConfigInfo)).Get<ConfigInfo>()??throw new ArgumentNullException(nameof(ConfigInfo));

    builder.Services.AddSingleton(config);
    string dbPath = Path.Combine($"{logFilePath}", "CQGBroadcast.db");
    logger.Information(dbPath);
	builder.Services.AddDbContext<AppDBContext>(option =>
	{
		option.UseSqlite($"Data Source={dbPath}");
	});
   
    builder.Services.AddSingleton<AESDecryptor>();
    builder.Services.AddSingleton<SqlHelper>();
    builder.Services.AddSingleton<NSEFeeder>();

    builder.Services.AddHttpClient<FileService>(cfg => cfg.BaseAddress = new Uri(fileUrl));

    builder.Services.AddSingleton<DataBroadCaster>();
    builder.Services.AddSingleton<InstrumentListener>();
    builder.Services.AddSingleton<DBFireBase>();
	builder.Services.AddHostedService<Worker>();
    builder.Services.AddWindowsService(option =>
    {
        option.ServiceName = "CQGFeederService";
    });

    var host = builder.Build();
	host.Run();
}
catch (Exception ex)
{
	logger.Error($"{ex.StackTrace}");
}
