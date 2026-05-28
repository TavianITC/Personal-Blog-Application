using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;

namespace Personal_Blog_Application.Tests.Helpers
{
    // Spins up a real EF Core DbContext backed by SQLite in-memory. Each test
    // class gets its own connection-scoped instance (data is wiped on Dispose).
    //
    // Why SQLite and not InMemory provider? It's an actual relational engine,
    // so it enforces foreign keys, transactions, and translates LINQ (incl.
    // EF.Functions.Like) much closer to SQL Server semantics than InMemory.
    public sealed class SqliteTestContext : IDisposable
    {
        public AppDbContext Db { get; }
        private readonly SqliteConnection _connection;

        public SqliteTestContext()
        {
            // :memory: DBs live only as long as the connection is open, so we
            // keep one connection open for the duration of the test.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            Db = new AppDbContext(options);
            Db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }
}
