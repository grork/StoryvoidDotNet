using Codevoid.Instapaper;
using Codevoid.Storyvoid.App.Implementations;
using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Codevoid.Storyvoid.Controls;

public sealed partial class AuthenticationControl : UserControl
{
    public Authenticator ViewModel { get; private set; }

    /// <summary>
    /// Raised when we have successfully authenticated, allowing listeners to
    /// react with an appropriate experience.
    /// </summary>
    public event EventHandler<ClientInformation>? SuccessfullyAuthenticated;

    public AuthenticationControl()
    {
        var settings = new AccountSettings();
        var accounts = new Accounts(settings.GetTokens()!);
        this.ViewModel = new Authenticator(accounts, settings);

        this.InitializeComponent();
    }

    private void Authenticate_Click(object sender, RoutedEventArgs e)
    {
        this.AuthenticateHelper();
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if(e.Key != VirtualKey.Enter)
        {
            return;
        }

        this.AuthenticateHelper();
    }

    /// <summary>
    /// Initiates authentication through the view model. Upon completion,
    /// ensures focus is placed somewhere sensible.
    /// </summary>
    private async void AuthenticateHelper()
    {
        var result = await this.ViewModel.Authenticate();
        if(result == null)
        {
            this.AccountTextBox.Focus(FocusState.Programmatic);
            return;
        }

        this.SuccessfullyAuthenticated?.Invoke(this, result!);
    }
}
