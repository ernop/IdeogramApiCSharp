using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Reflection;
using System.Timers;
using System.Text;
using System.Windows.Forms;
using static IdeogramAPIClient.TextUtils;

namespace IdeogramAPIClient
{
    public static class IdeogramUtils
    {
        public static void LogRequestAndResponse(string logFilePath, IdeogramGenerateRequest request, GenerateResponse response, string errorMessage = null, System.Net.HttpStatusCode? statusCode = null)
        {
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow,
                Request = request,
                Response = response,
                ErrorMessage = errorMessage,
                StatusCode = statusCode
            };

            var json = JsonConvert.SerializeObject(logEntry, Formatting.Indented);
            File.AppendAllText(logFilePath, json + Environment.NewLine + Environment.NewLine);
        }

        public static async Task SaveGeneratedImagesAsync(GenerateResponse response, IdeogramGenerateRequest request, IdeogramSettings settings)
        {
            if (!settings.SaveRawImage && !settings.SaveAnnotatedImage && !settings.SaveJsonLog)
            {
                return;
            }

            var timestamp = DateTime.UtcNow;
            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };

            foreach (var image in response.Data)
            {
                var fileName = $"{timestamp:yyyyMMddHHmmss}_{Guid.NewGuid()}";

                if (settings.SaveRawImage)
                {
                    await DownloadAndSaveImageAsync(image.Url, Path.Combine(settings.ImageDownloadFolder, $"{fileName}.png"));
                }

                if (settings.SaveAnnotatedImage)
                {
                    var imageBytes = await DownloadImageAsync(image.Url);
                    var textsToAnnotate = new List<Tuple<string,string>> { };
                    textsToAnnotate.AddRange(request.AnnotationTexts);

                    if (!string.IsNullOrEmpty(image.Prompt))
                    {
                        if (image.Prompt != request.Prompt)
                        {
                            textsToAnnotate.Add(new Tuple<string, string>("Rewritten by ideogram", image.Prompt));
                        }
                    }

                    var imageInfo = new Dictionary<string, string>
                    {
                        {"Model", request.Model.ToString()},
                        {"Seed", image.Seed.ToString()},
                        {"Safe", image.IsImageSafe.ToString()},
                        {"Generated", DateTime.Now.ToString()}
                    };

                    if (request.StyleType.HasValue)
                    {
                        imageInfo["Style"] = request.StyleType.ToString().ToLowerInvariant();
                    }

                    if (request.AspectRatio.HasValue)
                    {
                        imageInfo["AspectRatio"] = StringifyAspectRatio(request.AspectRatio.Value);
                    }
                    else if (request.Resolution.HasValue)
                    {
                        imageInfo["Resolution"] = request.Resolution.ToString();
                    }

                    if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
                    {
                        imageInfo["NegativePrompt"] = request.NegativePrompt;
                    }

                    await SaveImageAndAnnotateText(imageBytes, textsToAnnotate, imageInfo, Path.Combine(settings.ImageDownloadFolder, $"Annotated/{fileName}_annotated.png"));
                }

                if (settings.SaveJsonLog)
                {
                    var jsonLog = new
                    {
                        Timestamp = timestamp,
                        Request = request,
                        Response = image
                    };
                    File.WriteAllText(Path.Combine(settings.ImageDownloadFolder, $"{fileName}.json"), JsonConvert.SerializeObject(jsonLog, jsonSettings));
                }
            }
        }

        private static string StringifyAspectRatio(IdeogramAspectRatio ratio)
        {
            return ratio switch
            {
                IdeogramAspectRatio.ASPECT_10_16 => "10x16",
                IdeogramAspectRatio.ASPECT_16_10 => "16x10",
                IdeogramAspectRatio.ASPECT_9_16 => "9x16",
                IdeogramAspectRatio.ASPECT_16_9 => "16x9",
                IdeogramAspectRatio.ASPECT_3_2 => "3x2",
                IdeogramAspectRatio.ASPECT_2_3 => "2x3",
                IdeogramAspectRatio.ASPECT_4_3 => "4x3",
                IdeogramAspectRatio.ASPECT_3_4 => "3x4",
                IdeogramAspectRatio.ASPECT_1_1 => "1x1",
                IdeogramAspectRatio.ASPECT_1_3 => "1x3",
                IdeogramAspectRatio.ASPECT_3_1 => "3x1",
                _ => throw new ArgumentOutOfRangeException(nameof(ratio), ratio, null),
            };
        }

        private static async Task<byte[]> DownloadImageAsync(string imageUrl)
        {
            using var httpClient = new HttpClient();
            return await httpClient.GetByteArrayAsync(imageUrl);
        }

        private static async Task DownloadAndSaveImageAsync(string imageUrl, string outputPath)
        {
            var imageBytes = await DownloadImageAsync(imageUrl);
            File.WriteAllBytes(outputPath, imageBytes);
        }
    }
}