using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Instapaper
{
    public class Accounts
    {
        private readonly ClientInformation clientInformation;
        private readonly HttpClient client;

        public Accounts(ClientInformation clientInformation)
        {
            this.clientInformation = clientInformation;
            this.client = OAuthMessageHandler.CreateOAuthHttpClient(this.clientInformation);
        }

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

            var result = await this.client.PostAsync(Codevoid.Instapaper.Endpoints.Access.AccessToken, parameters);
            if (!result.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Instapaper service was unavailable");
            }

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
                this.clientInformation.ClientId,
                this.clientInformation.ClientSecret,
                token,
                secret);
        }
    }
}
