using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        // Set the working directory to where our current assembly is located.
        // This is because we want to _read_ datafiles co-located with the app
        // binaries + dependencies. However, windows now defaults to
        // %WINDIR%\System32. See more here:
        // https://github.com/microsoft/WindowsAppSDK/discussions/2195
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
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
            mainWindow = new MainWindow();
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
