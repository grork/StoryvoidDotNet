using System;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;
using Xunit;

namespace Codevoid.Test.Instapaper
{
    public class AuthenticationTests
    {
        [Fact]
        public async Task CanGetAccessToken()
        {
            var clientInfo = new ClientInformation(
                InstapaperAPIKey.CLIENT_ID,
                InstapaperAPIKey.CLIENT_SECRET
            );
            var accounts = new Accounts(clientInfo);
            var clientInfoWithAccessToken = await accounts.GetAccessTokenAsync(
                InstapaperAPIKey.INSTAPAPER_ACCOUNT,
                InstapaperAPIKey.INSTAPAPER_PASSWORD
            );

            Assert.NotNull(clientInfoWithAccessToken); // Client info wasn't returned
            Assert.Equal(clientInfo.ClientId, clientInfoWithAccessToken.ClientId); // Client ID didn't match
            Assert.Equal(clientInfo.ClientSecret, clientInfoWithAccessToken.ClientSecret); // Client Secret didn't match
            Assert.False(String.IsNullOrWhiteSpace(clientInfoWithAccessToken.Token)); // Token missing
            Assert.False(String.IsNullOrWhiteSpace(clientInfoWithAccessToken.TokenSecret)); // Secret Missing
        }

        [Fact]
        public async Task CanVerifyCredentials()
        {
            var clientInfo = new ClientInformation(
                InstapaperAPIKey.CLIENT_ID,
                InstapaperAPIKey.CLIENT_SECRET,
                InstapaperAPIKey.ACCESS_TOKEN,
                InstapaperAPIKey.TOKEN_SECRET
            );

            var accounts = new Accounts(clientInfo);
            var userInformation = await accounts.VerifyCredentials();

            Assert.NotNull(userInformation); // User info wasn't returned
            Assert.Equal(InstapaperAPIKey.INSTAPAPER_USER_ID, userInformation.UserId); // User ID didn't match
            Assert.Equal(InstapaperAPIKey.INSTAPAPER_ACCOUNT, userInformation.Username); // Username didn't match
        }
    }
}