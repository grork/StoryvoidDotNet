using System.ComponentModel;
using System.Runtime.CompilerServices;
using Codevoid.Instapaper;

namespace Codevoid.Storyvoid.ViewModels;

/// <summary>
/// Manages authentication lifecycle, including enablement of login,
/// verifiication, error handling, and cached-credential restoration.
/// </summary>
public class Authenticator : INotifyPropertyChanged
{
    private IAccounts accountsService;
    private string _email;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Password to be used to verify credentials
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Instantiates the class.
    /// </summary>
    /// <param name="accountsService">
    /// Accounts service to use to verify & validate
    /// </param>
    public Authenticator(IAccounts accountsService)
    {
        this.accountsService = accountsService;
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
            if(this._email == value)
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
}
