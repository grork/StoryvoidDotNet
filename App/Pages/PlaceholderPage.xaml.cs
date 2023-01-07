using Codevoid.Storyvoid.Sync;
using Codevoid.Storyvoid.Utilities;
using Codevoid.Storyvoid.ViewModels.Commands;
using Microsoft.UI.Xaml.Navigation;

namespace Codevoid.Storyvoid.Pages;

internal record PlaceholderParameter(
    object? Parameter,
    Task<(IFolderDatabase Folders, SyncHelper Sync)> DataLayer,
    IList<string> OperationLog
);

/// <summary>
/// Page that serves as a placeholder for other pages that have not yet been
/// implemented. Displays the parameter supplied, or a number. Additionally
/// provides access to some simple generic operations such as back, forward,
/// and clearing of the stack. Allows clearing of the saved account too.
/// </summary>
internal sealed partial class PlaceholderPage : Page
{
    private IAppUtilities? utilities;
    private IFolderDatabase? folders;
    public IList<string>? OperationLog { get; private set; }

    public PlaceholderPage()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var args = (NavigationParameter)e.Parameter;

        this.utilities = args.Utilities;
        var placeholderParam = (PlaceholderParameter)(args.Parameter!);
        var parameter = placeholderParam.Parameter;

        this.ParameterContent.Text = (parameter is not null) ? parameter.ToString() : "No Parameter";
        this.OperationLog = placeholderParam.OperationLog;

        base.OnNavigatedTo(e);

        var dataLayer = await placeholderParam.DataLayer;
        this.folders = dataLayer.Folders;
        this.SyncButton.Command = new SyncCommand(dataLayer.Sync);
    }

    private void GoBack_Click(object sender, RoutedEventArgs e) => this.Frame.GoBack();

    private void GoForward_Click(object sender, RoutedEventArgs e) => this.Frame.GoForward();

    private void NewPlaceholder_Click(object sender, RoutedEventArgs e) => this.utilities?.ShowPlaceholder();

    private void Signout_Click(object sender, RoutedEventArgs e)
    {
        this.utilities?.Signout();
    }

    private void ClearStack_Click(object sender, RoutedEventArgs e)
    {
        this.Frame.BackStack.Clear();
        this.Frame.ForwardStack.Clear();
    }

    private void CreateRandomFolder_Click(object sender, RoutedEventArgs e) => this.folders?.CreateFolder(DateTime.Now.Ticks.ToString());

    private void ShowLogin_Click(object sender, RoutedEventArgs e)
    {
        this.utilities?.ShowLogin();
    }
}