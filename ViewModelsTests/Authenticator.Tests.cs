using Codevoid.Storyvoid.ViewModels;
using Codevoid.Utilities.OAuth;
using System.Windows.Input;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class AuthenticatorTests
{
    private Authenticator authenticator;
    private IAccountSettings settings;
    private MockAccountService accountService;

    private void AssertCanExecuteChangedRaised(Action action, bool expectedCanExecuteValue)
    {
        var wasRaised = false;
        this.authenticator.CanExecuteChanged += (o, a) => wasRaised = true;

        action();

        Assert.True(wasRaised, "CanExecute was not raised");
        if(wasRaised)
        {
            Assert.Equal(expectedCanExecuteValue, this.authenticator.CanExecute(null));
        }
    }

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
    public void CanExecuteChangedNotRaisedIfAssignedValueIsSameAsCurrentValue()
    {
        this.authenticator.Email = "test@example.com";
        this.authenticator.CanExecuteChanged += (_, _) => Assert.Fail("Should not have event raised");

        this.authenticator.Email = "test@example.com";
    }

    [Fact]
    public void CanVerifyRaisesPropertyChangedWhenEmailSetFromDefaultState()
    {
        Assert.PropertyChanged(this.authenticator, "CanVerify", () => this.authenticator.Email = "test@example.com");
    }

    [Fact]
    public void CanExecuteChangedRaisedWhenEmailSetFromDefaultState()
    {
        this.AssertCanExecuteChangedRaised(() => this.authenticator.Email = "test@example.com", true);
    }

    [Fact]
    public void CanVerifyRaisesPropertyChangedWhenEmailSetFromExistingValue()
    {
        this.authenticator.Email = "test2@example.com";
        Assert.PropertyChanged(this.authenticator, "CanVerify", () => this.authenticator.Email = "test@example.com");
    }

    [Fact]
    public void CanExecuteChangedRaisedWhenEmailSetFromExistingValue()
    {
        this.authenticator.Email = "test2@example.com";
        this.AssertCanExecuteChangedRaised(() => this.authenticator.Email = "test@example.com", true);
    }

    [Fact]
    public void CanVerifyRaisesPropertyChangedWhenEmailSetToEmpty()
    {
        this.authenticator.Email = "test@example.com";
        Assert.PropertyChanged(this.authenticator, "CanVerify", () => this.authenticator.Email = String.Empty);
    }

    [Fact]
    public void CanVerifyRaisesCanExecuteChangedWhenEmailSetToEmpty()
    {
        this.authenticator.Email = "test@example.com";
        this.AssertCanExecuteChangedRaised(() => this.authenticator.Email = String.Empty, false);
    }

    [Fact]
    public async void AttemptingToAuthenticateWhenNotReadyToVerifyThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => this.authenticator.Authenticate());
    }

    [Fact]
    public async void AuthenticatingWithValidCredentialsReturnsValidClientInformation()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;

        var clientInfo = await this.authenticator.Authenticate();
        Assert.NotNull(clientInfo);
    }
    
    [Fact]
    public async void AuthenticatingWithValidCredentialsRaisesSuccessfullyAuthenticatedEvent()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;
        ClientInformation? clientInfoFromEvent = null;

        this.authenticator.SuccessfullyAuthenticated += (o, e) => clientInfoFromEvent = e;

        var clientInformation = await this.authenticator.Authenticate();
        Assert.NotNull(clientInfoFromEvent);
        Assert.Equal(clientInformation, clientInfoFromEvent);
    }

    [Fact]
    public async void AuthenticatingWithUsingICommandInterfaceRaisesSuccessfullyAuthenticatedEvent()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;
        var completionSource = new TaskCompletionSource<ClientInformation>();

        this.authenticator.SuccessfullyAuthenticated += (o, e) => completionSource.SetResult(e);

        this.authenticator.Execute(null);

        var clientInformation = await completionSource.Task;
        Assert.NotNull(clientInformation);
    }

    [Fact]
    public async void AuthenticatingWithValidCredentialsReturnsValidClientInformationMatchingSavedInformation()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;

        Assert.False(this.settings.HasTokens);

        var clientInfo = await this.authenticator.Authenticate();
        Assert.NotNull(clientInfo);

        Assert.True(this.settings.HasTokens);
        Assert.Equal(this.settings.GetTokens(), clientInfo);
    }

    [Fact]
    public async void AuthenticatingWithValidCredentialsSetsEmptyErrorMessage()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;

        _ = await this.authenticator.Authenticate();

        Assert.NotNull(this.authenticator.FriendlyErrorMessage);
        Assert.Equal(0, this.authenticator.FriendlyErrorMessage.Length);
    }

    [Fact]
    public async void AuthenticatingSetsWorkingToTrueAndThenFalseWithSuccessfulAuthentication()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;

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
    public async void AuthenticatingSetsCanVerifyToTrueAndThenFalseWithSuccessfulAuthentication()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;

        var isWorkingSetToTrue = false;
        var canVerifySetToFalse = false;

        this.authenticator.PropertyChanged += (_, name) =>
        {
            if ((name.PropertyName == "IsWorking") && this.authenticator.IsWorking)
            {
                isWorkingSetToTrue = true;
            }

            if((name.PropertyName == "CanVerify") && this.authenticator.IsWorking)
            {
                canVerifySetToFalse = !this.authenticator.CanVerify;
            }
        };

        _ = await this.authenticator.Authenticate();

        Assert.True(isWorkingSetToTrue);
        Assert.True(canVerifySetToFalse);
        Assert.False(this.authenticator.IsWorking);
        Assert.True(this.authenticator.CanVerify);
    }

    [Fact]
    public async void AuthenticatingWithInvalidCredentialsDoesNotReturnClientInformation()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;

        var clientInfo = await this.authenticator.Authenticate();
        Assert.Null(clientInfo);
    }

    [Fact]
    public async void AuthenticatingWithInvalidCredentialsDoesNotReturnClientInformationAndSetsEmptyTokens()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;

        Assert.False(this.settings.HasTokens);

        var clientInfo = await this.authenticator.Authenticate();
        Assert.Null(clientInfo);

        Assert.False(this.settings.HasTokens);
        var clientInformation = this.settings.GetTokens();
        Assert.Null(clientInformation.Token);
        Assert.Null(clientInformation.TokenSecret);
    }

    [Fact]
    public async void AuthenticatingWithInvalidCredentialsSetsWorkingToTrueAndThenFalseWithUnsuccessfulAuthentication()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;

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
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;

        var clientInfo = await this.authenticator.Authenticate();
        Assert.False(String.IsNullOrEmpty(this.authenticator.FriendlyErrorMessage));
    }
    
    [Fact]
    public async void AuthenticatingWithInvalidCredentialsDoesNotRaiseSuccessfulEvent()
    {
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        var successfulEventRaised = false;

        this.authenticator.SuccessfullyAuthenticated += (o, e) => successfulEventRaised = true;

        var clientInfo = await this.authenticator.Authenticate();
        Assert.False(successfulEventRaised, "Event shouldn't have been raised");
    }

    [Fact]
    public async void TimingOutRequestsClearsIsWorkingAndSetsAppropriateErrorMessage()
    {
        this.accountService.TimeoutRequests = true;
        this.authenticator.Email = MockAccountService.FAKE_ACCOUNT;
        this.authenticator.Password = MockAccountService.FAKE_PASSWORD;

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