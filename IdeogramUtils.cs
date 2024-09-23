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

namespace IdeogramAPIClient
{
    public static class IdeogramUtils
    {
        public static void LogRequestAndResponse(string logFilePath, GenerateRequest request, GenerateResponse response, string errorMessage = null, System.Net.HttpStatusCode? statusCode = null)
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

        public static async Task SaveGeneratedImagesAsync(GenerateResponse response, GenerateRequest request, IdeogramSettings settings)
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
                    if (request.Prompt.Contains("___"))
                    {
                        request.Prompt = request.Prompt.Split("___")[0];
                    }
                    var textsToAnnotate = new List<string> { request.Prompt};
                    if (!string.IsNullOrEmpty(image.Prompt))
                    {
                        var negativeText = "";
                        if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
                        { 
                            negativeText = $" Negative Prompt: {request.NegativePrompt}";
                        }
                        var sizeText = "";
                        if (request.AspectRatio.HasValue)
                        {
                            sizeText = $" AspectRatio: {StringifyAspectRatio(request.AspectRatio.Value)}";
                        }else if (request.Resolution.HasValue)
                        {
                            sizeText = $"Resolution: {request.Resolution}";
                        }
                        var simplifiedStyleTypeName = request.StyleType.ToString().ToLowerInvariant();
                        //prune off my secret outer prompt.
                        if (image.Prompt.Contains("___"))
                        {
                            Console.WriteLine("BEfore"+image.Prompt);
                            image.Prompt = image.Prompt.Split("___")[0];
                            Console.WriteLine("AFter"+image.Prompt);
                        }                       
                        textsToAnnotate.Add($"Generated prompt: {image.Prompt}");
                        textsToAnnotate.Add($"{sizeText} Model: {request.Model} Seed: {image.Seed} Safe: {image.IsImageSafe} Style: {simplifiedStyleTypeName}{negativeText} Generated: {DateTime.Now}");
                    }
                    await SaveImageAndAnnotateText(imageBytes, textsToAnnotate, Path.Combine(settings.ImageDownloadFolder, $"Annotated/{fileName}_annotated.png"));
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

        public static async Task SaveImageAndAnnotateText(byte[] imageBytes, List<string> texts, string outputPath)
        {
            using var ms = new MemoryStream(imageBytes);
            using var originalImage = Image.FromStream(ms);
            int textHeight = CalculateTextHeight(texts);
            int newHeight = originalImage.Height + textHeight;

            using (var annotatedImage = new Bitmap(originalImage.Width, newHeight))
            using (var graphics = Graphics.FromImage(annotatedImage))
            {
                graphics.Clear(Color.Black);
                graphics.DrawImage(originalImage, 0, 0);

                using (var font = new Font("Arial", 12, FontStyle.Regular))
                using (var brush = new SolidBrush(Color.White))
                {
                    float y = originalImage.Height + 4;
                    float leftMargin = 3;
                    float rightMargin = annotatedImage.Width - 12;

                    foreach (var text in texts)
                    {
                        DrawWrappedText(graphics, text, font, brush, leftMargin, rightMargin, ref y);
                    }
                }

                annotatedImage.Save(outputPath, ImageFormat.Png);
            }
        }

        public static int CalculateTextHeight(List<string> texts)
        {
            int height = 20; // Initial padding
            using (var font = new Font("Arial", 12, FontStyle.Regular))
            using (var bitmap = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                float maxWidth = 1000; // Assume a reasonable max width
                foreach (var text in texts)
                {
                    string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        height += MeasureTextHeight(graphics, line, font, maxWidth);
                    }
                    height += 5; // Spacing between texts
                }
            }
            return height;
        }

        private static int MeasureTextHeight(Graphics graphics, string text, Font font, float maxWidth)
        {
            string[] words = text.Split(' ');
            string line = "";
            int lineCount = 1;
            foreach (string word in words)
            {
                string testLine = line + word + " ";
                SizeF size = graphics.MeasureString(testLine, font);
                if (size.Width > maxWidth)
                {
                    lineCount++;
                    line = word + " ";
                }
                else
                {
                    line = testLine;
                }
            }
            return (int)(lineCount * font.GetHeight());
        }

        private static void DrawWrappedText(Graphics graphics, string text, Font font, Brush brush, float x, float maxWidth, ref float y)
        {
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                string[] words = line.Split(' ');
                string currentLine = "";
                foreach (string word in words)
                {
                    string testLine = currentLine + word + " ";
                    SizeF size = graphics.MeasureString(testLine, font);
                    if (size.Width > maxWidth)
                    {
                        graphics.DrawString(currentLine, font, brush, x, y);
                        y += font.GetHeight();
                        currentLine = word + " ";
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }
                graphics.DrawString(currentLine, font, brush, x, y);
                y += font.GetHeight();
            }
            y += 5; // Spacing between texts
        }
    }
}