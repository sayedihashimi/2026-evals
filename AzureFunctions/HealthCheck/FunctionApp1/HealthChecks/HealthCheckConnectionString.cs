using Microsoft.Data.Sqlite;

namespace FunctionApp1.HealthChecks;

internal static class HealthCheckConnectionString
{
    public static string RewriteToAbsoluteSqlitePath(string rawConnectionString, string absoluteDbPath)
    {
        var csb = new SqliteConnectionStringBuilder(rawConnectionString);

        csb.DataSource = absoluteDbPath;
        return csb.ToString();
    }
}
