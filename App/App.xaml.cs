﻿using Codevoid.Storyvoid.Utilities;
using Microsoft.UI.Text;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace Codevoid.Storyvoid.App;
using Strings = Codevoid.Storyvoid.Resources;

/// <summary>
/// Main entrypoint into the application that handles deciding what initial UI
/// to handle, and any non-simple-app-launch handling.
/// </summary>
public partial class Launcher : Application
{
    public Launcher()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // We can't run without API Keys, and people won't read the readme, so
        // check if we've got placeholder API Keys, and show the appropriate
        // message if we do. Normal app startup shouldn't happen in this
        // scenario.
        Window? mainWindow = null;
#pragma warning disable CS0162
        if (InstapaperAPIKey.CONSUMER_KEY_SECRET == "PLACEHOLDER"
         || InstapaperAPIKey.CONSUMER_KEY == "PLACEHOLDER")
        {
            mainWindow = this.GetNoApiKeysWindow();
        }
        else
        {
            // Initiate DB opening on a separate thread, so that by the time we
            // actually want it, it shuld be complete.
            var dbTask = Task.Run(AppUtilities.OpenDatabaseAsync);
            mainWindow = new MainWindow(dbTask);
#pragma warning restore
        }

        // Source the window title from the manifest, so we have a single source
        mainWindow.Title = AppInfo.Current.DisplayInfo.DisplayName;
        mainWindow.Activate();
    }

    private Window GetNoApiKeysWindow()
    {
        var errorMessage = Strings.Errors.NoApiKeysSet;
        Debug.Fail(errorMessage); // Force break into the debugger

        return new Window()
        {
            Content = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 72,
                FontWeight = FontWeights.ExtraLight,
                Text = errorMessage
            }
        };
    }
}
