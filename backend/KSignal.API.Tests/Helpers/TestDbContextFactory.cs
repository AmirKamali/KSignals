using KSignal.API.Data;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Tests.Helpers;

public static class TestDbContextFactory
{
    public static KalshiDbContext CreateInMemoryContext(string databaseName = "")
    {
        var dbName = string.IsNullOrEmpty(databaseName)
            ? Guid.NewGuid().ToString()
            : databaseName;

        var options = new DbContextOptionsBuilder<KalshiDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new KalshiDbContext(options);

        // Ensure the database is created
        context.Database.EnsureCreated();

        return context;
    }
}
