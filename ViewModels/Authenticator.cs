using System.ComponentModel;
using System.Runtime.CompilerServices;
using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Read &amp; write account settings e.g., Instapaper Tokens.
///
/// It is expected that the implementor stores them in a secure manner.
/// </summary>
public interface IAccountSettings
{
    bool HasTokens { get; }
    ClientInformation? GetTokens();
    void SetTokens(ClientInformation tokens);
    void ClearTokens();
}

/// <summary>
/// Manages authentication lifecycle, including enablement of login,
/// verifiication, error handling, and cached-credential restoration.
/// </summary>
public class Authenticator : INotifyPropertyChanged
{
    private readonly IAccounts accountsService;
    private readonly IAccountSettings settings;
    private string email;
    private bool isWorking = false;
    private string friendlyErrorMessage = String.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Instantiates the class.
    /// </summary>
    /// <param name="accountsService">
    /// Accounts service to use to verify & validate
    /// </param>
    public Authenticator(IAccounts accountsService, IAccountSettings settings)
    {
        this.accountsService = accountsService;
        this.settings = settings;
        this.email = String.Empty;
        this.Password = String.Empty;
    }

    /// <summary>
    /// Password to be used to verify credentials.
    ///
    /// Note, this can be an empty string since Instapaper does not require
    /// passwords for their accounts.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// When working to obtain tokens, this will be set to true. In all other
    /// situations, it will be false, including failure cases.
    /// </summary>
    public bool IsWorking
    {
        get => this.isWorking;
        private set
        {
            if (this.isWorking == value)
            {
                return;
            }

            this.isWorking = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(this.CanVerify));
        }
    }

    /// <summary>
    /// User-displayable message indicating the reason for a failure (if there
    /// is, in fact, a failure)
    /// </summary>
    public string FriendlyErrorMessage
    {
        get => this.friendlyErrorMessage;
        private set
        {
            if (this.friendlyErrorMessage == value)
            {
                return;
            }

            this.friendlyErrorMessage = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Raise PropertyChanged. By default (E.g. no parameter supplied), the
    /// callers member name (method, property) will be used as the property name
    /// </summary>
    /// <param name="propertyName">
    /// If provided, the name of the property to raise for. If not, derived from
    /// the immediate callers method/property name.
    /// </param>
    private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
    {
        var handler = this.PropertyChanged;
        handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Does this instance have enough information to be able to attempt
    /// verificiation of these credentials
    /// </summary>
    public bool CanVerify => !String.IsNullOrWhiteSpace(this.Email) && !this.IsWorking;

    /// <summary>
    /// E-mail (E.g. account name) to be used when verifying the account.
    /// </summary>
    public string Email
    {
        get => this.email;
        set
        {
            if (this.email == value)
            {
                return;
            }

            this.email = value;
            this.RaisePropertyChanged();

            // Verification capability might change; make callers request it to
            // work out if they can verify.
            this.RaisePropertyChanged(nameof(this.CanVerify));
        }
    }

    /// <summary>
    /// Authenticates with the service, and if successful returns the tokens to
    /// the caller, whilst also persisting them to the settings storage
    /// </summary>
    /// <returns>Tokens derived from the authentication request</returns>
    /// <exception cref="InvalidOperationException">If the object is not in a state that can attempt to authenticate</exception>
    public async Task<ClientInformation?> Authenticate()
    {
        if (!this.CanVerify)
        {
            throw new InvalidOperationException("No e-email provided or already trying to authenticate");
        }

        ClientInformation? clientInformation = null;
        try
        {
            this.IsWorking = true;
            this.FriendlyErrorMessage = String.Empty;
            clientInformation = await this.accountsService.GetAccessTokenAsync(this.Email, this.Password);
            this.settings.SetTokens(clientInformation);
        }
        catch (AuthenticationFailedException)
        {
            this.FriendlyErrorMessage = Resources.AuthenticationFailed_Message;
        }
        catch (TaskCanceledException e) when (e.InnerException != null && e.InnerException.GetType() == typeof(TimeoutException))
        {
            this.FriendlyErrorMessage = Resources.AuthenticationTimeout_Message;
        }
        finally
        {
            this.IsWorking = false;
        }

        return clientInformation;
    }
}