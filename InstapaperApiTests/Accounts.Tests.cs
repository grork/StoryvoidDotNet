using System;
using System.Threading.Tasks;
using Codevoid.Instapaper;
using Codevoid.Utilities.OAuth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codevoid.Test.Instapaper
{
    [TestClass]
    public class AuthenticationTests
    {
        [TestMethod]
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

            Assert.IsNotNull(clientInfoWithAccessToken, "Client info wasn't returned");
            Assert.AreEqual(clientInfo.ClientId, clientInfoWithAccessToken.ClientId, "Client ID didn't match");
            Assert.AreEqual(clientInfo.ClientSecret, clientInfoWithAccessToken.ClientSecret, "Client Secret didn't match");
            Assert.IsFalse(String.IsNullOrWhiteSpace(clientInfoWithAccessToken.Token), "Token missing");
            Assert.IsFalse(String.IsNullOrWhiteSpace(clientInfoWithAccessToken.TokenSecret), "Secret Missing");
        }

        [TestMethod]
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

            Assert.IsNotNull(userInformation, "User info wasn't returned");
            Assert.AreEqual(InstapaperAPIKey.INSTAPAPER_USER_ID, userInformation.UserId, "User ID didn't match");
            Assert.AreEqual(InstapaperAPIKey.INSTAPAPER_ACCOUNT, userInformation.Username, "Username didn't match");
        }
    }
}