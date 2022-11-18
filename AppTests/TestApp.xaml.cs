using Microsoft.VisualStudio.TestPlatform.TestExecutor;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.ComponentModel;

[assembly:WinUITestTarget(typeof(Codevoid.Test.Storyvoid.App))]

namespace Codevoid.Test.Storyvoid;

public partial class App : Application
{
    internal static App? Instance { get; private set; }

    public App()
    {
        App.Instance = this;
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        UnitTestClient.CreateDefaultUI();

        TestWindow = new TestWindowWithFrame();
        TestWindow.Activate();

        UITestMethodAttribute.DispatcherQueue = TestWindow.DispatcherQueue;

        // Replace back with e.Arguments when https://github.com/microsoft/microsoft-ui-xaml/issues/3368 is fixed
        UnitTestClient.Run(Environment.CommandLine);
    }

    internal TestWindowWithFrame? TestWindow { get; private set; }
}

internal class TestWindowWithFrame : Window
{
    internal readonly Frame Frame;
    internal TestWindowWithFrame(): base()
    {
        this.Frame = new Frame();
    }
}