using Codevoid.Storyvoid.ViewModels;

namespace Codevoid.Test.Storyvoid.ViewModels;

public class AuthenticatorTests
{
    private Authenticator authenticator;

    public AuthenticatorTests()
    {
        this.authenticator = new Authenticator(new MockAccountService());
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
}