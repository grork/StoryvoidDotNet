using Microsoft.VisualStudio.TestPlatform.TestExecutor;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

[assembly:WinUITestTarget(typeof(Codevoid.Test.Storyvoid.App))]

namespace Codevoid.Test.Storyvoid;

public partial class App : Application
{
    public App() => this.InitializeComponent();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        UnitTestClient.CreateDefaultUI();

        m_window = new TestWindow();
        m_window.Activate();

        UITestMethodAttribute.DispatcherQueue = m_window.DispatcherQueue;

        // Replace back with e.Arguments when https://github.com/microsoft/microsoft-ui-xaml/issues/3368 is fixed
        UnitTestClient.Run(Environment.CommandLine);
    }

    private Window? m_window;
}