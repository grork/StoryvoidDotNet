using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Utilities;
using Microsoft.UI.Xaml.Navigation;

namespace Codevoid.Storyvoid.Pages;

/// <summary>
/// Page that serves as a placeholder for other pages that have not yet been
/// implemented. Displays the parameter supplied, or a number. Additionally
/// provides access to some simple generic operations such as back, forward,
/// and clearing of the stack. Allows clearing of the saved account too.
/// </summary>
internal sealed partial class PlaceholderPage : Page
{
    private IAppUtilities? utilities;
    public PlaceholderPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        object? parameter = null;
        var args = e.Parameter as NavigationParameter;
        if(args != null)
        {
            this.utilities = args.Utilities;
            parameter = args.Parameter;
        }

        this.ParameterContent.Text = (parameter != null) ? parameter.ToString() : "No Parameter";
    }

    private void GoBack_Click(object sender, RoutedEventArgs e) => this.Frame.GoBack();
    
    private void GoForward_Click(object sender, RoutedEventArgs e) => this.Frame.GoForward();

    private void NewPlaceholder_Click(object sender, RoutedEventArgs e) => this.utilities?.ShowPlaceholder();

    private void ClearCredentials_Click(object sender, RoutedEventArgs e)
    {
        var accountSettings = new AccountSettings();
        accountSettings.ClearTokens();
    }

    private void ClearStack_Click(object sender , RoutedEventArgs e)
    {
        this.Frame.BackStack.Clear();
        this.Frame.ForwardStack.Clear();
    }
}