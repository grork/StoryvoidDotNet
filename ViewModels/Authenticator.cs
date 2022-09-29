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
    private string _email;
    private bool _isWorking = false;
    private string _friendlyErrorMessage = String.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

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
        get => this._isWorking;
        private set
        {
            if (this._isWorking == value)
            {
                return;
            }

            this._isWorking = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// User-displayable message indicating the reason for a failure (if there
    /// is, in fact, a failure)
    /// </summary>
    public string FriendlyErrorMessage
    {
        get => this._friendlyErrorMessage;
        private set
        {
            if (this._friendlyErrorMessage == value)
            {
                return;
            }

            this._friendlyErrorMessage = value;
            this.RaisePropertyChanged();
        }
    }

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
        this._email = String.Empty;
        this.Password = String.Empty;
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
    public bool CanVerify => !String.IsNullOrWhiteSpace(this.Email);

    /// <summary>
    /// E-mail (E.g. account name) to be used when verifying the account.
    /// </summary>
    public string Email
    {
        get => this._email;
        set
        {
            if (this._email == value)
            {
                return;
            }

            this._email = value;
            this.RaisePropertyChanged();

            // Verification capability might change; make callers request it to
            // work out if they can verify.
            this.RaisePropertyChanged("CanVerify");
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
            throw new InvalidOperationException("No e-email provided");
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