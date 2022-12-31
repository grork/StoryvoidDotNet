using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Codevoid.Storyvoid.App.Implementations;

/// <summary>
/// Stores account settings in windows settings container. Implements <see
/// cref="IAccountSettings"/> for use with view models.
/// </summary>
internal class AccountSettings : IAccountSettings
{
    // Settings key names. These match the WWA verison to ease migration
    private static readonly string TOKEN_CONTAINER_KEY = "usertokens";
    private static readonly string TOKEN_TOKEN_KEY = "token";
    private static readonly string TOKEN_SECRET_KEY = "secret";
    private ClientInformation? clientInformation = null;

    public bool HasTokens
    {
        get
        {
            var stored = this.GetTokens();
            return (stored.Token is not null && stored.TokenSecret is not null);
        }
    }

    public ClientInformation GetTokens()
    {
        if (this.clientInformation is null)
        {
            var tokenSetting = ApplicationData.Current.LocalSettings.Values[TOKEN_CONTAINER_KEY] as ApplicationDataCompositeValue;
            String? tokenValue = null;
            String? tokenSecret = null;
            if (tokenSetting is not null)
            {
                tokenValue = tokenSetting[TOKEN_TOKEN_KEY] as String;
                tokenSecret = tokenSetting[TOKEN_SECRET_KEY] as String;
            }

            var newClientInformation = new ClientInformation(
                InstapaperAPIKey.CONSUMER_KEY,
                InstapaperAPIKey.CONSUMER_KEY_SECRET,
                tokenValue,
                tokenSecret
            );

            newClientInformation.ProductVersion = AppInfo.Current.Package.Id.Version.ToString()!;
            newClientInformation.ProductName = AppInfo.Current.DisplayInfo.DisplayName;
            this.clientInformation = newClientInformation;
        }

        return clientInformation;
    }

    public void SetTokens(ClientInformation tokens)
    {
        var tokenSetting = new ApplicationDataCompositeValue();
        tokenSetting[TOKEN_TOKEN_KEY] = tokens.Token;
        tokenSetting[TOKEN_SECRET_KEY] = tokens.TokenSecret;

        ApplicationData.Current.LocalSettings.Values[TOKEN_CONTAINER_KEY] = tokenSetting;
        this.clientInformation = null;
    }

    public void ClearTokens()
    {
        ApplicationData.Current.LocalSettings.Values.Remove(TOKEN_CONTAINER_KEY);
        this.clientInformation = null;
    }
}
