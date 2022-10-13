using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;
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

    public bool HasTokens
    {
        get
        {
            var stored = this.GetStoredTokenAndSecret();
            return (stored.Token != null && stored.Secret != null);
        }
    }

    private (string? Token, string? Secret) GetStoredTokenAndSecret()
    {
        var tokenSetting = ApplicationData.Current.LocalSettings.Values[TOKEN_CONTAINER_KEY] as ApplicationDataCompositeValue;
        String? tokenValue = null;
        String? tokenSecret = null;
        if (tokenSetting != null)
        {
            tokenValue = tokenSetting[TOKEN_TOKEN_KEY] as String;
            tokenSecret = tokenSetting[TOKEN_SECRET_KEY] as String;
        }

        return (tokenValue, tokenSecret);
    }

    public ClientInformation? GetTokens()
    {
        var (tokenValue, tokenSecret) = this.GetStoredTokenAndSecret();
        return new ClientInformation(InstapaperAPIKey.CONSUMER_KEY,
            InstapaperAPIKey.CONSUMER_KEY_SECRET,
            tokenValue,
            tokenSecret);
    }

    public void SetTokens(ClientInformation tokens)
    {
        var tokenSetting = new ApplicationDataCompositeValue();
        tokenSetting[TOKEN_TOKEN_KEY] = tokens.Token;
        tokenSetting[TOKEN_SECRET_KEY] = tokens.TokenSecret;

        ApplicationData.Current.LocalSettings.Values[TOKEN_CONTAINER_KEY] = tokenSetting;
    }

    public void ClearTokens()
    {
        ApplicationData.Current.LocalSettings.Values.Remove(TOKEN_CONTAINER_KEY);
    }
}
