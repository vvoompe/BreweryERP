using BreweryERP.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BreweryERP.Tests;

/// <summary>
/// Фабрика тестового DbContext з InMemory provider.
/// Придушує TransactionIgnoredWarning, бо InMemory не підтримує транзакції —
/// але це нормально для unit-тестів (атомарність не перевіряємо тут).
/// </summary>
public static class TestDbFactory
{
    public static ApplicationDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
                w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }
}
