using Codevoid.Storyvoid.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Codevoid.Storyvoid.Controls;

public sealed partial class AuthenticationControl : UserControl
{
    public Authenticator ViewModel { get; private set; }

    public AuthenticationControl(Authenticator authenticator)
    {
        this.ViewModel = authenticator;

        this.InitializeComponent();
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
    }
}
