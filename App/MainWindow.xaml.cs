using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.Controls;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace Codevoid.Storyvoid.App;

public sealed partial class MainWindow : Window
{
    private readonly IAccountSettings settings = new AccountSettings();
    public MainWindow()
    {
        this.InitializeComponent();

        if (settings.HasTokens)
        {
            this.SwitchToSignedIn();
            return;
        }

        this.SwitchToSignedOut();
    }

    private void AuthenticationControl_SuccessfullyAuthenticated(object? sender, Utilities.OAuth.ClientInformation e)
    {
        var authenticator = sender as AuthenticationControl;
        authenticator!.SuccessfullyAuthenticated -= AuthenticationControl_SuccessfullyAuthenticated;
        this.SwitchToSignedIn();
    }

    private void ClearCredentials_Click(object sender, RoutedEventArgs e)
    {
        this.settings.ClearTokens();
        this.SwitchToSignedOut();
    }

    private void SwitchToSignedOut()
    {
        var authenticationControl = new AuthenticationControl();
        authenticationControl.SuccessfullyAuthenticated += AuthenticationControl_SuccessfullyAuthenticated;
        this.Content = authenticationControl;
    }

    private void SwitchToSignedIn()
    {
        var label = new TextBlock()
        {
            Text = "Have Creds",
            FontSize = 72,
            FontWeight = FontWeights.ExtraLight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var button = new Button()
        {
            Content = "Clear Credentials"
        };
        button.Click += ClearCredentials_Click;

        var content = new StackPanel();
        content.Children.Add(label);
        content.Children.Add(button);

        this.Content = content;
    }
}
