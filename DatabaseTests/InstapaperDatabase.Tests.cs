﻿using Codevoid.Storyvoid;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid;

public sealed class InstapaperDatabaseTests
{
    [Fact]
    public async Task CanOpenDatabase()
    {
        using var db = await TestUtilities.GetDatabase();
        Assert.NotNull(db);
    }

    [Fact]
    public async Task CanReopenDatabase()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var first = new InstapaperDatabase(connection);
        await first.OpenOrCreateDatabaseAsync();

        var second = new InstapaperDatabase(connection);
        await first.OpenOrCreateDatabaseAsync();
    }
}