using Codevoid.Instapaper;
using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.ViewModels;
using Microsoft.UI.Xaml;

namespace Codevoid.Storyvoid.App;

public sealed partial class MainWindow : Window
{
    private readonly IAccountSettings settings = new AccountSettings();
    public MainWindow()
    {
        this.InitializeComponent();
        var accounts = new Accounts(settings.GetTokens()!);
        this.ViewModel = new Authenticator(accounts, settings);

        if (settings.HasTokens)
        {
            this.AlreadyAuthedTextBlock.Text = "Already Has Credentials";
        }
    }

    public Authenticator ViewModel { get; private set; }

    private async void Authenticate_Click(object sender, RoutedEventArgs e)
    {
        if (!this.ViewModel.CanVerify)
        {
            return;
        }

        await this.ViewModel.Authenticate();
    }

    private void ClearCredentials_Click(object sender, RoutedEventArgs e)
    {
        this.settings.ClearTokens();
    }
}
