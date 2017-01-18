namespace EmergencyServicesBot
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class MicrosoftCognitiveSpeechService
    {
        public string DefaultLocale { get; } = "en-US";

        /// <summary>
        /// Gets text from an audio stream.
        /// </summary>
        /// <param name="audiostream"></param>
        /// <returns>Transcribed text. </returns>
        public async Task<string> GetTextFromAudioAsync(Stream audiostream)
        {
            var requestUri = string.Format(
                "/recognize?scenarios=smd&appid=D4D52672-91D7-4C74-8AD8-42B1D98141A5&locale={0}&device.os=bot&form=BCSSTT&version=3.0&format=json&instanceid=565D69FF-E928-4B7E-87DA-9A750B96D9E3&requestid={1}",
                DefaultLocale,
                Guid.NewGuid());

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://speech.platform.bing.com");

                var token = Authentication.Instance.GetAccessToken();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);

                try
                {
                    using (var binaryContent = new ByteArrayContent(StreamToBytes(audiostream)))
                    {
                        var response = await client.PostAsync(requestUri, binaryContent);
                        var responseString = await response.Content.ReadAsStringAsync();
                        dynamic data = JsonConvert.DeserializeObject(responseString);

                        if (data != null)
                        {
                            return data.results.name;
                        }
                        {
                            return string.Empty;
                        }
                    }
                }
                catch (Exception exp)
                {
                    Debug.WriteLine(exp);
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Converts Stream into byte[].
        /// </summary>
        /// <param name="input">Input stream</param>
        /// <returns>Output byte[]</returns>
        private static byte[] StreamToBytes(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}