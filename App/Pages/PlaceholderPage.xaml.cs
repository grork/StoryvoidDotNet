using Codevoid.Storyvoid.App.Implementations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Codevoid.Storyvoid.Pages;

public sealed partial class PlaceholderPage : Page
{
    private static int pageCounter = 1;
    public PlaceholderPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        this.ParameterContent.Text = (e.Parameter != null) ? e.Parameter.ToString() : "No Parameter";
    }

    private void GoBack_Click(object sender, RoutedEventArgs e) => this.Frame.GoBack();
    private void GoForward_Click(object sender, RoutedEventArgs e) => this.Frame.GoForward();

    private void NewPlaceholder_Click(object sender, RoutedEventArgs e) => this.Frame.Navigate(typeof(PlaceholderPage), pageCounter++);

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