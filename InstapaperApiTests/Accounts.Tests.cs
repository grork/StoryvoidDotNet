using System;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.Ordering;

namespace Codevoid.Test.Instapaper
{
    public sealed class AuthenticationTests
    {
        private readonly ITestOutputHelper outputHelper;
        public AuthenticationTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        [Fact]
        public async Task CanGetAccessToken()
        {
            TestUtilities.ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.CONSUMER_KEY, nameof(InstapaperAPIKey.CONSUMER_KEY));
            TestUtilities.ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.CONSUMER_KEY_SECRET, nameof(InstapaperAPIKey.CONSUMER_KEY_SECRET));
            var clientInfoWithoutAccessToken = new ClientInformation(
                InstapaperAPIKey.CONSUMER_KEY,
                InstapaperAPIKey.CONSUMER_KEY_SECRET
            );

            var accounts = new Accounts(clientInfoWithoutAccessToken);

            TestUtilities.ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.INSTAPAPER_ACCOUNT, nameof(InstapaperAPIKey.INSTAPAPER_ACCOUNT));
            TestUtilities.ThrowIfValueIsAPIKeyHasntBeenSet(InstapaperAPIKey.INSTAPAPER_PASSWORD, nameof(InstapaperAPIKey.INSTAPAPER_PASSWORD));
            var clientInfoWithAccessToken = await accounts.GetAccessTokenAsync(
                InstapaperAPIKey.INSTAPAPER_ACCOUNT,
                InstapaperAPIKey.INSTAPAPER_PASSWORD
            );

            Assert.NotNull(clientInfoWithAccessToken); // Client info wasn't returned
            Assert.Equal(clientInfoWithoutAccessToken.ConsumerKey, clientInfoWithAccessToken.ConsumerKey); // Client ID didn't match
            Assert.Equal(clientInfoWithoutAccessToken.ConsumerKeySecret, clientInfoWithAccessToken.ConsumerKeySecret); // Client Secret didn't match
            Assert.False(String.IsNullOrWhiteSpace(clientInfoWithAccessToken.Token)); // Token missing
            Assert.False(String.IsNullOrWhiteSpace(clientInfoWithAccessToken.TokenSecret)); // Secret Missing

            this.outputHelper.WriteLine("Token Information for this request:");
            this.outputHelper.WriteLine("Token: {0}", clientInfoWithAccessToken.Token);
            this.outputHelper.WriteLine("Token Secret: {0}", clientInfoWithAccessToken.TokenSecret);
        }

        [Fact]
        public async Task CanVerifyCredentials()
        {
            var clientInfo = TestUtilities.GetClientInformation();
            var accounts = new Accounts(clientInfo);
            var userInformation = await accounts.VerifyCredentialsAsync();

            Assert.Equal(InstapaperAPIKey.INSTAPAPER_USER_ID, userInformation.UserId); // User ID didn't match
            Assert.Equal(InstapaperAPIKey.INSTAPAPER_ACCOUNT, userInformation.Username); // Username didn't match
        }
    }
}