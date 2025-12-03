using CQGAPI;
using CQGAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CQGFeederMatchTrader.Data;

public class AppDBContext : DbContext
{
    public AppDBContext(DbContextOptions<AppDBContext> options):base(options)
    {
        RelationalDatabaseCreator databaseCreator=(RelationalDatabaseCreator)Database.GetService<IDatabaseCreator>();
        if (!databaseCreator.Exists())
            databaseCreator.Create();
        if (!databaseCreator.HasTables())
            databaseCreator.CreateTables();
    }
    public DbSet<DBRate> CurrentRates { get; set; }
}
