using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.IO;

namespace IdeogramAPIClient
{
    public class IdeogramClient
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.ideogram.ai";

        public IdeogramSettings Settings { get; }

        public IdeogramClient(IdeogramSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Api-Key", Settings.IdeogramApiKey);
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        public async Task<GenerateResponse> GenerateImageAsync(GenerateRequest request)
        {
            var jsonRequest = JsonConvert.SerializeObject(new { image_request = request }, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter> { new StringEnumConverter(camelCaseText: false) }
            });

            var httpContent = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/generate", httpContent);
            
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var generateResponse = JsonConvert.DeserializeObject<GenerateResponse>(content);
            foreach (var ii in generateResponse.Data)
            {
                Console.WriteLine($"AFter response, input was: {request.Prompt} and OUTPUT was => {ii.Prompt}");
                if (ii.Prompt.Length < 20)
                {
                    var a = 4;
                }
            }
                if (Settings.EnableLogging)
            {
                IdeogramUtils.LogRequestAndResponse(Settings.LogFilePath, request, generateResponse);
            }

            if (Settings.SaveRawImage)
            {
                await IdeogramUtils.SaveGeneratedImagesAsync(generateResponse, request, Settings);
            }

            return generateResponse;
        }        
    }
}
