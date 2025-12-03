using Serilog.Events;
using Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace CQGAPI.Helpers;

public static class HostBuilderExtentions
{


    public static Serilog.Core.Logger AddSerilogServices(this HostApplicationBuilder builder, string fileDir)
    {
        string logDir = Path.Combine(fileDir, "Logs");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
        var logger = new LoggerConfiguration()
                     .WriteTo.Console()
                     .WriteTo.File(Path.Combine(logDir, "Log.txt"), rollingInterval: RollingInterval.Day)
                     .MinimumLevel.Information()
                     .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // Disable Microsoft logs below Warning
                     .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning) // Disable EF Core SQL logs below Warning
                                                                                                                     //.WriteTo.Console()
                     .CreateLogger();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger);
        return logger;
    }

}
