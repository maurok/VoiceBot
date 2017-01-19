namespace EmergencyServicesBot
{
    using System;
    using System.IdentityModel.Tokens;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Configuration;

    public sealed class Authentication
    {
        private static readonly string ApiKey;
        private AccessTokenInfo token;
        private ManualResetEvent tokenRefreshing = new ManualResetEvent(true);

        static Authentication()
        {
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
        public async Task<AccessTokenInfo> GetAccessTokenAsync(bool forceRefresh = false)
        {
            // Token will be null first time the function is called.
            if (forceRefresh || this.token == null)
            {
                tokenRefreshing.WaitOne();

                tokenRefreshing.Set();
                try
                {
                    if (forceRefresh || this.token == null)
                    {
                        await this.RefreshTokenAsync();
                    }
                }
                finally
                {
                    tokenRefreshing.Reset();
                }
            }

            return this.token;
        }

        /// <summary>
        /// Issues a new AccessToken from the Speech Api
        /// </summary>
        /// This method couldn't be async because we are calling it inside of a lock.
        /// <returns>AccessToken</returns>
        private async Task<AccessTokenInfo> GetNewTokenAsync()
        {
            using (var client = new HttpClient())
            {
                var content = new StringContent(string.Empty);
                content.Headers.Add("Ocp-Apim-Subscription-Key", ApiKey);

                var response = await client.PostAsync("https://api.cognitive.microsoft.com/sts/v1.0/issueToken", content);

                var jwtToken = await response.Content.ReadAsStringAsync();

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
        private async Task RefreshTokenAsync()
        {
            // TODO: Better check 403 on request and renew there (remove timer)?

            this.token = await GetNewTokenAsync();
            //this.timer?.Dispose();
            //this.timer = new Timer(
            //    async x => await this.RefreshTokenAsync(),
            //    null,
            //    CalculateDueTimeForTimer(this.token.expires_in), // Specifies the delay before RefreshToken is invoked.
            //    TimeSpan.FromMilliseconds(-1)); // Indicates that this function will only run once
        }

        //private static TimeSpan CalculateDueTimeForTimer(int secondsFromEpoch)
        //{
        //    DateTime dueTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secondsFromEpoch);
        //    return (dueTime - DateTime.UtcNow).Subtract(TimeSpan.FromMinutes(1));
        //}
    }
}