using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class MockAccountSettings : IAccountSettings
{
    private ClientInformation tokens = new ClientInformation("FAKE KEY", "FAKE SECRET");

    public void ClearTokens() => this.tokens = new ClientInformation("FAKE KEY", "FAKE SECRET");
    public ClientInformation GetTokens() => this.tokens;
    public void SetTokens(ClientInformation tokens) => this.tokens = tokens;
    public bool HasTokens => (this.tokens.Token is not null && this.tokens.TokenSecret is not null);
}

public class MockArticleListSettings : IArticleListSettings
{
    public string SortIdentifier { get; set; } = ArticleList.DefaultSortIdentifier;
}