using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class MockAccountService : IAccounts
{
    public Task<ClientInformation> GetAccessTokenAsync(string username, string password)
    {
        throw new NotImplementedException();
    }

    public Task<UserInformation> VerifyCredentialsAsync()
    {
        throw new NotImplementedException();
    }
}

