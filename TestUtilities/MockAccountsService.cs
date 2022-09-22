using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class MockAccountService : IAccounts
{
    public static readonly string FAKE_ACCOUNT = "fake@example.com";
    public static readonly string FAKE_PASSWORD = "1234";

    public bool TimeoutRequests { get; set; }

    public Task<ClientInformation> GetAccessTokenAsync(string username, string password)
    {
        if ((username != FAKE_ACCOUNT) || (password != FAKE_PASSWORD))
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