﻿using System;
using System.Data;
using Codevoid.Storyvoid;
using Codevoid.Storyvoid.Sync;
using Codevoid.Storyvoid.ViewModels.Commands;
using Microsoft.Data.Sqlite;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class SyncCommandTests : IDisposable
{
    private SyncCommand command;
    private SyncHelper helper;
    private MockInstapaperSync mockSync;
    private MockArticleDownloader mockDownloader;

    public SyncCommandTests()
    {
        this.mockSync = new MockInstapaperSync();
        this.mockDownloader = new MockArticleDownloader();
        this.helper = new SyncHelper(mockDownloader, () =>
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();


            return Task.FromResult<(IInstapaperSync, IDbConnection)>((this.mockSync, connection));
        });

        this.command = new SyncCommand(this.helper);
    }

    public void Dispose()
    {
        this.command.Dispose();
    }

    private void TriggerSyncingComplete()
    {
        this.mockDownloader.TriggerAll();
        this.mockSync.TriggerSyncEverythingComplete();
    }

    [Fact]
    public void CanInstantiateCommand()
    {
        Assert.NotNull(this.command);
    }

    [Fact]
    public void CanExecuteReturnsTrueWhenNotSyncing()
    {
        Assert.True(this.command.CanExecute(null));
    }

    [Fact]
    public async Task CanExecuteReturnsFalseWhenSyncing()
    {
        var syncTask = this.helper.SyncDatabaseAndArticles();

        Assert.False(this.command.CanExecute(null));

        this.TriggerSyncingComplete();

        await syncTask;
    }

    [Fact]
    public async Task CanExecuteReturnsTrueAfterSyncingComplete()
    {
        this.TriggerSyncingComplete();
        await this.helper.SyncDatabaseAndArticles();

        Assert.True(this.command.CanExecute(null));
    }

    [Fact]
    public async Task CanExexcuteChangedRaisedCorrectlyDuringSync()
    {
        var completionSource = new TaskCompletionSource();
        var canExecuteResults = new List<bool>();
        this.command.CanExecuteChanged += (o, a) =>
        {
            canExecuteResults.Add(this.command.CanExecute(null));
            if (canExecuteResults.Count == 2)
            {
                completionSource.SetResult();
            }
        };

        this.TriggerSyncingComplete();

        this.command.Execute(null);

        await completionSource.Task;

        Assert.Equal(2, canExecuteResults.Count);
        Assert.False(canExecuteResults[0]);
        Assert.True(canExecuteResults[1]);
    }
}

