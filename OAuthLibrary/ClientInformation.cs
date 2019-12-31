using System;
using System.Net.Http.Headers;

namespace Codevoid.Utilities.OAuth
{
    public class ClientInformation
    {
        private string productName = "Codevoid OAuth Library";
        private string productVersion = "1.0";
        private ProductInfoHeaderValue userAgentValue;

        public readonly string ClientId;
        public readonly string ClientSecret;
        public readonly string Token;
        public readonly string TokenSecret;

        public ClientInformation(string clientId,
                                 string clientSecret,
                                 string token = null,
                                 string tokenSecret = null)
        {
            if (String.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentNullException(nameof(clientId),
                                               "Client ID is required");
            }

            if (String.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentNullException(nameof(clientSecret),
                                                "Client Secret is required");
            }

            this.ClientId = clientId;
            this.ClientSecret = clientSecret;
            this.Token = token;
            this.TokenSecret = tokenSecret;
        }

        public string ProductName
        {
            get { return this.productName; }
            set
            {
                this.productName = value;
                this.userAgentValue = null;
            }
        }

        public string ProductVersion
        {
            get { return this.productVersion; }
            set
            {
                this.productVersion = value;
                this.userAgentValue = null;
            }
        }

        public ProductInfoHeaderValue UserAgent
        {
            get
            {
                if (this.userAgentValue == null)
                {
                    this.userAgentValue = new ProductInfoHeaderValue(this.productName, this.productVersion);
                }

                return this.userAgentValue;
            }
        }
    }
}
