using System;
using System.Windows.Input;
using Codevoid.Storyvoid.Sync;

namespace Codevoid.Storyvoid.ViewModels.Commands;

/// <summary>
/// Initiates app-wide syncing of the database &amp; article contents
/// </summary>
public class SyncCommand : ICommand, IDisposable
{
    public event EventHandler? CanExecuteChanged;
    private SyncHelper helper;

    /// <summary>
    /// Instantiates a command wrapping a given instance of the sync helper. It
    /// is expected -- but not required -- that this be a shared instance for a
    /// given database.
    /// </summary>
    /// <param name="helper"></param>
    public SyncCommand(SyncHelper helper)
    {
        this.helper = helper;
        this.helper.PropertyChanged += Helper_PropertyChanged;
    }

    private void Helper_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        this.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        this.helper.PropertyChanged -= Helper_PropertyChanged;
    }

    private void RaiseCanExecuteChanged()
    {
        this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    // When syncing, we should be disabled so as not to initiate another sync
    public bool CanExecute(object? parameter) => !this.helper.IsSyncing;

    public async void Execute(object? parameter)
    {
        await this.helper.SyncDatabaseAndArticles();
    }
}