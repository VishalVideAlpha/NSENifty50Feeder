using CQGAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

using System.Data;


namespace CQGAPI.Helpers
{
    public class SqlHelper
    {
        private readonly string _conString;
        private readonly ILogger<SqlHelper> _logger;
        public SqlHelper(ILogger<SqlHelper> logger, IConfiguration configuration)
        {
            _logger = logger;
            _conString = configuration.GetConnectionString("default") ?? throw new ArgumentNullException(nameof(_conString));
        }

       
    }
}
