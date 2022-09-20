using Codevoid.Instapaper;
using Codevoid.Test.Instapaper;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class MockAccountService : IAccounts
{
    public bool TimeoutRequests { get; set; }

    public Task<ClientInformation> GetAccessTokenAsync(string username, string password)
    {
        if ((username != InstapaperAPIKey.INSTAPAPER_ACCOUNT) || (password != InstapaperAPIKey.INSTAPAPER_PASSWORD))
        {
            throw new AuthenticationFailedException();
        }

        if (this.TimeoutRequests)
        {
            throw new TaskCanceledException("Ded", new TimeoutException());
        }

        return Task.FromResult(new ClientInformation("CONSUMERKEY", "CONSUMERSECRET", "TOKEN", "TOKENSECRET"));
    }

    public Task<UserInformation> VerifyCredentialsAsync()
    {
        throw new NotImplementedException();
    }
}