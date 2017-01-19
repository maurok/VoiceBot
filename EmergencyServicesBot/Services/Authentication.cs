namespace EmergencyServicesBot
{
    using System;
    using System.IdentityModel.Tokens;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Web.Configuration;

    public sealed class Authentication
    {
        private static readonly object LockObject;
        private static readonly string ApiKey;
        private AccessTokenInfo token;
        private Timer timer;

        static Authentication()
        {
            LockObject = new object();
            ApiKey = WebConfigurationManager.AppSettings["MicrosoftSpeechApiKey"];
        }

        private Authentication()
        {
        }

        public static Authentication Instance { get; } = new Authentication();

        /// <summary>
        /// Gets the current access token.
        /// </summary>
        /// <returns>Current access token</returns>
        public AccessTokenInfo GetAccessToken()
        {
            // Token will be null first time the function is called.
            if (this.token == null)
            {
                lock (LockObject)
                {
                    // This condition will be true only once in the lifetime of the application
                    if (this.token == null)
                    {
                        this.RefreshToken();
                    }
                }
            }

            return this.token;
        }

        /// <summary>
        /// Issues a new AccessToken from the Speech Api
        /// </summary>
        /// This method couldn't be async because we are calling it inside of a lock.
        /// <returns>AccessToken</returns>
        private AccessTokenInfo GetNewToken()
        {
            using (var client = new HttpClient())
            {
                var content = new StringContent(string.Empty);
                content.Headers.Add("Ocp-Apim-Subscription-Key", ApiKey);

                var response = client.PostAsync("https://api.cognitive.microsoft.com/sts/v1.0/issueToken", content).Result;

                var jwtToken = response.Content.ReadAsStringAsync().Result;

                var tokenDetails = new JwtSecurityTokenHandler().ReadToken(jwtToken) as JwtSecurityToken;

                return new AccessTokenInfo
                {
                    access_token = jwtToken,
                    expires_in = Convert.ToInt32(tokenDetails.Claims.First(c => c.Type == "exp").Value),
                    scope = tokenDetails.Claims.First(c => c.Type == "scope").Value,
                    token_type = (string)tokenDetails.Header.First(h => h.Key == "typ").Value
                };
            }
        }

        /// <summary>
        /// Refreshes the current token before it expires. This method will refresh the current access token.
        /// It will also schedule itself to run again before the newly acquired token's expiry by one minute.
        /// </summary>
        private void RefreshToken()
        {
            // TODO: Better check 403 on request and renew there (remove timer)?

            this.token = GetNewToken();
            this.timer?.Dispose();
            this.timer = new Timer(
                x => this.RefreshToken(),
                null,
                GetExpirationTimer(this.token.expires_in).Subtract(TimeSpan.FromMinutes(1)), // Specifies the delay before RefreshToken is invoked.
                TimeSpan.FromMilliseconds(-1)); // Indicates that this function will only run once
        }

        private static TimeSpan GetExpirationTimer(int secondsFromEpoch)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime target = origin.AddSeconds(secondsFromEpoch);

            var ts = target - DateTime.UtcNow;
            return ts;
        }
    }
}