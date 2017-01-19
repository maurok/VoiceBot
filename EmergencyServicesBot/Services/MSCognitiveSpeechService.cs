namespace EmergencyServicesBot
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Bing.Speech;
    using Newtonsoft.Json;

    public class MicrosoftCognitiveSpeechService
    {
        private const string DefaultLocale = "en-US";

        #region resolve speech using Bing NS classes
        private static readonly Uri ShortPhraseUrl = new Uri(@"wss://speech.platform.bing.com/api/service/recognition");

        private static readonly Uri LongDictationUrl = new Uri(@"wss://speech.platform.bing.com/api/service/recognition/continuous");

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly Task recognitionResultAck = Task.FromResult(true);

        public async Task<string> GetTextAsync(Stream audioStream)
        {
            var tcs = new TaskCompletionSource<string>();

            // create the preferences object
            var preferences = new Preferences(DefaultLocale, LongDictationUrl, new CognitiveServicesAuthorizationProvider());

            // Create a a speech client
            using (var speechClient = new SpeechClient(preferences))
            {
                var recognizedPhrases = new List<string>();

                //speechClient.SubscribeToPartialResult(this.OnPartialResult);
                speechClient.SubscribeToRecognitionResult(result =>
                {
                    if (result.Phrases != null & result.Phrases.Count > 0)
                    {
                        // first get high or normal, if not get any
                        var phrase = result.Phrases.FirstOrDefault(p => Confidence.High.Equals(p.Confidence));
                        if (phrase == null)
                        {
                            phrase = result.Phrases.FirstOrDefault(p => Confidence.Normal.Equals(p.Confidence));
                        }

                        if (phrase == null)
                        {
                            phrase = result.Phrases.First();
                        }

                        // store recognized phrases here to return all when finished
                        recognizedPhrases.Add(phrase.DisplayText);

                        // TODO: how to know when recognition finished?
                        // actions: track some elapsed time? - check other API available?
                        // for now send back first sentence recognized and do not process others
                        if (!TaskStatus.RanToCompletion.Equals(tcs.Task.Status))
                        {
                            tcs.SetResult(string.Join(string.Empty, recognizedPhrases.ToArray()));
                        }
                    }

                    return recognitionResultAck;
                });

                // create an audio content and pass it a stream.
                var deviceMetadata = new DeviceMetadata(DeviceType.Near, DeviceFamily.Desktop, NetworkType.Ethernet, OsName.Windows, "1607", "Dell", "T3600");
                var applicationMetadata = new ApplicationMetadata("SampleApp", "1.0.0");
                var requestMetadata = new RequestMetadata(Guid.NewGuid(), deviceMetadata, applicationMetadata, "SampleAppService");

                await speechClient.RecognizeAsync(new SpeechInput(audioStream, requestMetadata), this.cts.Token).ConfigureAwait(false);
            }

            return await tcs.Task;
        }
        #endregion

        #region resolve speech using REST API
        private const string SpeechRecognitionUri = "https://speech.platform.bing.com";

        private const string RecognizeOperationUri = "/recognize?scenarios=smd&appid=D4D52672-91D7-4C74-8AD8-42B1D98141A5&locale={0}&device.os=bot&form=BCSSTT&version=3.0&format=json&instanceid=565D69FF-E928-4B7E-87DA-9A750B96D9E3&requestid={1}";

        /// <summary>
        /// Gets text from an audio stream.
        /// </summary>
        /// <param name="audiostream"></param>
        /// <returns>Transcribed text. </returns>
        public async Task<string> GetTextFromAudioAsync(Stream audiostream, int retryCount = 0)
        {
            // if we are retrying then refresh auth token, but do not retry more than once
            if (retryCount > 0)
            {
                await Authentication.Instance.GetAccessTokenAsync(true);

                if (retryCount > 1)
                {
                    return string.Empty;
                }
            }

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(SpeechRecognitionUri);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + await Authentication.Instance.GetAccessTokenAsync());

                try
                {
                    using (var binaryContent = new ByteArrayContent(StreamToBytes(audiostream)))
                    {
                        var requestUri = string.Format(RecognizeOperationUri, DefaultLocale, Guid.NewGuid());

                        var response = await client.PostAsync(requestUri, binaryContent);
                        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            return await GetTextFromAudioAsync(audiostream, ++retryCount);
                        }

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
        #endregion
    }
}