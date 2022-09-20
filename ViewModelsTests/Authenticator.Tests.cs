using Codevoid.Storyvoid.ViewModels;
using Codevoid.Test.Instapaper;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class AuthenticatorTests
{
    private Authenticator authenticator;
    private IAccountSettings settings;
    private MockAccountService accountService;

    public AuthenticatorTests()
    {
        this.accountService = new MockAccountService();
        this.settings = new MockAccountSettings();
        this.authenticator = new Authenticator(this.accountService, this.settings);
    }

    [Fact]
    public void EmptyEmailCantVerify()
    {
        Assert.False(this.authenticator.CanVerify, "Shouldn't be able to verify with no email");
    }

    [Fact]
    public void CanVerifyWithEmailAndEmptyPassword()
    {
        this.authenticator.Email = "test@example.com";
        Assert.True(this.authenticator.CanVerify, "Should be able to verify with email & empty password");
    }

    [Fact]
    public void CanVerifyWithEmailAndNonEmptyPassword()
    {
        this.authenticator.Email = "test@example.com";
        this.authenticator.Password = "password";
        Assert.True(this.authenticator.CanVerify, "Should be able to verify with email & password");
    }

    [Fact]
    public void CanVerifyWithEmailAndWhitespacePassword()
    {
        this.authenticator.Email = "test@example.com";
        this.authenticator.Password = "\t\t";
        Assert.True(this.authenticator.CanVerify, "Should be able to verify with email & password");
    }

    [Fact]
    public void CanVerifyPropertyChangedNotRaisedIfAssignedValueIsSameAsCurrentValue()
    {
        this.authenticator.Email = "test@example.com";
        this.authenticator.PropertyChanged += (_, _) => Assert.Fail("Property change event shouldn't be raised");

        this.authenticator.Email = "test@example.com";
    }

    [Fact]
    public void CanVerifyRaisesPropertyChangedWhenEmailSetFromDefaultState()
    {
        Assert.PropertyChanged(this.authenticator, "CanVerify", () => this.authenticator.Email = "test@example.com");
    }

    [Fact]
    public void CanVerifyRaisesPropertyChangedWhenEmailSetFromExistingValue()
    {
        this.authenticator.Email = "test2@example.com";
        Assert.PropertyChanged(this.authenticator, "CanVerify", () => this.authenticator.Email = "test@example.com");
    }

    [Fact]
    public void CanVerifyRaisesPropertyChangedWhenEmailSetToEmpty()
    {
        this.authenticator.Email = "test@example.com";
        Assert.PropertyChanged(this.authenticator, "CanVerify", () => this.authenticator.Email = String.Empty);
    }

    [Fact]
    public async void AttemptingToAuthenticateWhenNotReadyToVerifyThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => this.authenticator.Authenticate());
    }

    [Fact]
    public async void AuthenticatingWithValidCredentialsReturnsValidClientInformation()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;
        this.authenticator.Password = InstapaperAPIKey.INSTAPAPER_PASSWORD;

        var clientInfo = await this.authenticator.Authenticate();
        Assert.NotNull(clientInfo);
    }

    [Fact]
    public async void AuthenticatingWithValidCredentialsReturnsValidClientInformationMatchingSavedInformation()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;
        this.authenticator.Password = InstapaperAPIKey.INSTAPAPER_PASSWORD;

        Assert.False(this.settings.HasTokens);

        var clientInfo = await this.authenticator.Authenticate();
        Assert.NotNull(clientInfo);

        Assert.True(this.settings.HasTokens);
        Assert.Equal(this.settings.GetTokens(), clientInfo);
    }

    [Fact]
    public async void AuthenticatingWithValidCredentialsSetsEmptyErrorMessage()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;
        this.authenticator.Password = InstapaperAPIKey.INSTAPAPER_PASSWORD;

        _ = await this.authenticator.Authenticate();

        Assert.NotNull(this.authenticator.FriendlyErrorMessage);
        Assert.Equal(0, this.authenticator.FriendlyErrorMessage.Length);
    }

    [Fact]
    public async void AuthenticatingSetsWorkingToTrueAndThenFalseWithSuccessfulAuthentication()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;
        this.authenticator.Password = InstapaperAPIKey.INSTAPAPER_PASSWORD;

        var isWorkingSetToTrue = false;

        this.authenticator.PropertyChanged += (_, name) =>
        {
            if ((name.PropertyName == "IsWorking") && this.authenticator.IsWorking)
            {
                isWorkingSetToTrue = true;
            }
        };

        _ = await this.authenticator.Authenticate();

        Assert.True(isWorkingSetToTrue);
        Assert.False(this.authenticator.IsWorking);
    }

    [Fact]
    public async void AuthenticatingWithInvalidCredentialsDoesNotReturnClientInformation()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;

        var clientInfo = await this.authenticator.Authenticate();
        Assert.Null(clientInfo);
    }

    [Fact]
    public async void AuthenticatingWithInvalidCredentialsDoesNotReturnClientInformationAndSetsEmptyTokens()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;

        Assert.False(this.settings.HasTokens);

        var clientInfo = await this.authenticator.Authenticate();
        Assert.Null(clientInfo);

        Assert.False(this.settings.HasTokens);
        Assert.Null(this.settings.GetTokens());
    }

    [Fact]
    public async void AuthenticatingWithInvalidCredentialsSetsWorkingToTrueAndThenFalseWithUnsuccessfulAuthentication()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;

        var isWorkingSetToTrue = false;
        this.authenticator.PropertyChanged += (_, name) =>
        {
            if ((name.PropertyName == "IsWorking") && this.authenticator.IsWorking)
            {
                isWorkingSetToTrue = true;
            }
        };

        _ = await this.authenticator.Authenticate();

        Assert.True(isWorkingSetToTrue);
        Assert.False(this.authenticator.IsWorking);
    }

    [Fact]
    public async void AuthenticatingWithInvalidCredentialsSetsErrorMessage()
    {
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;

        var clientInfo = await this.authenticator.Authenticate();
        Assert.False(String.IsNullOrEmpty(this.authenticator.FriendlyErrorMessage));
    }

    [Fact]
    public async void TimingOutRequestsClearsIsWorkingAndSetsAppropriateErrorMessage()
    {
        this.accountService.TimeoutRequests = true;
        this.authenticator.Email = InstapaperAPIKey.INSTAPAPER_ACCOUNT;
        this.authenticator.Password = InstapaperAPIKey.INSTAPAPER_PASSWORD;

        var isWorkingSetToTrue = false;

        this.authenticator.PropertyChanged += (_, name) =>
        {
            if ((name.PropertyName == "IsWorking") && this.authenticator.IsWorking)
            {
                isWorkingSetToTrue = true;
            }
        };

        var clientInformation = await this.authenticator.Authenticate();

        Assert.Null(clientInformation);
        Assert.True(isWorkingSetToTrue);
        Assert.False(this.authenticator.IsWorking);
        Assert.False(String.IsNullOrEmpty(this.authenticator.FriendlyErrorMessage));
    }
}