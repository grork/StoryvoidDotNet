using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class MockAccountSettings : IAccountSettings
{
    private ClientInformation? tokens = null;

    public void ClearTokens() => this.tokens = null;
    public ClientInformation? GetTokens() => this.tokens;
    public void SetTokens(ClientInformation tokens) => this.tokens = tokens;
    public bool HasTokens => this.tokens != null;
}

public class MockArticleListSettings : IArticleListSettings
{
    public string SortIdentifier { get; set; } = ArticleList.DefaultSortIdentifier;
}