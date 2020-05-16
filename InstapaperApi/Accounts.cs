using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Instapaper
{
    /// <summary>
    /// The information, returned from Instapaper, representing the User
    /// Information for the user that made the request.
    /// </summary>
    public readonly struct UserInformation
    {
        public readonly ulong UserId;
        public readonly string Username;

        /// <summary>
        /// Does the user have an active subscription with Instapaper
        /// </summary>
        public readonly bool HasSubscription;

        internal UserInformation(
            ulong userId,
            string username,
            bool hasSubscription
        )
        {
            this.UserId = userId;
            this.Username = username;
            this.HasSubscription = hasSubscription;
        }
    }

    /// <summary>
    /// Accounts API for Instapaper -- getting tokens, verifying creds.
    /// </summary>
    public class Accounts
    {
        private readonly ClientInformation clientInformation;
        private readonly HttpClient client;

        /// <summary>
        /// Constructs instance using the supplied OAuth client information
        /// </summary>
        /// <param name="clientInformation">Information to sign requests with</param>
        public Accounts(ClientInformation clientInformation)
        {
            this.clientInformation = clientInformation;
            this.client = OAuthMessageHandler.CreateOAuthHttpClient(clientInformation);
        }

        /// <summary>
        /// Gets a full access token + secret for the supplied credentials
        /// </summary>
        /// <returns><see cref="ClientInformation"/> with authentication tokens</returns>
        public async Task<ClientInformation> GetAccessTokenAsync(string username, string password)
        {
            var parameters = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    { "x_auth_username", username },
                    { "x_auth_password", password },
                    { "x_auth_mode", "client_auth" }
                }
            );

            var result = await this.client.PostAsync(Codevoid.Instapaper.EndPoints.Access.AccessToken, parameters);
            result.EnsureSuccessStatusCode();

            var body = await result.Content.ReadAsStringAsync();
            var payload = Microsoft.AspNetCore.WebUtilities.FormReader.ReadForm(body);

            if (!payload.TryGetValue("oauth_token_secret", out var secret))
            {
                throw new InvalidOperationException("No secret returned");
            }

            if (!payload.TryGetValue("oauth_token", out var token))
            {
                throw new InvalidOperationException("No token returned");
            }

            return new ClientInformation(
                this.clientInformation.ConsumerKey,
                this.clientInformation.ConsumerKeySecret,
                token,
                secret);
        }

        /// <summary>
        /// Verify the authentication token stored by this instance.
        /// </summary>
        /// <returns>User information if successful</returns>
        public async Task<UserInformation> VerifyCredentials()
        {
            var payload = new FormUrlEncodedContent(new Dictionary<string, string>());
            var result = await this.client.PostAsync(EndPoints.Access.VerifyCredentials, payload);
            result.EnsureSuccessStatusCode();

            var document = JsonDocument.Parse(await result.Content.ReadAsStreamAsync());
            var userInfoElement = document.RootElement[0];

            var userId = userInfoElement.GetProperty("user_id").GetUInt64();
            var username = userInfoElement.GetProperty("username").ToString();
            var hasSubscription = userInfoElement.GetProperty("subscription_is_active").ToString();

            return new UserInformation(userId, username, (hasSubscription == "1" ? true : false));
        }
    }
}
