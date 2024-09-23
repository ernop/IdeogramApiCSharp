using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

using System.IO;

using IdeogramAPIClient;
using System.Text.RegularExpressions;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Anthropic.SDK.Constants;

namespace Dalle3
{
    public class Program
    {
        private static List<string> GetPrompts(IdeogramSettings settings)
        {
            return File.ReadAllLines(settings.LoadPromptsFrom)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
        }

        static async Task Main(string[] args)
        {
            var settingsFilePath = "ideogram-settings.json";
            var settings = IdeogramSettings.LoadFromFile(settingsFilePath);
            settings.Validate();

            var prompts = GetPrompts(settings);

            var myRun = new IdeogramRun();
            myRun.RandomizeOrder = true;

            myRun.CleanPrompts = (string x) =>
            {
                x = Regex.Replace(x, " -both ", "")
                    .Replace(" -h ", "")
                    .Replace(" -hd ", "")
                    .Replace(" -vivid ", "")
                    .Replace(" -both ", "");
                x = x.Trim(',').Trim().Trim('\'').Trim('"').Trim();
                return x;
            };

            myRun.Filter = (string x) =>
            {
                var parts = x.Split(" ");
                if (parts.Length < 3) return false;
                if (x.Length < 10) return false;
                if (x.Contains(",,")) return false;
                if (x.Contains("[[")) return false;
                if (x.Contains("{{")) return false;
                return true;
            };

            myRun.ImageCreationLimit = 150;
            myRun.CopiesPer = 1;
            myRun.PromptVariants = new List<string> {
                //"Please imagine and then describe a beautiful clear photo based on the following subject. Your inspirations are: Tolkien, Medieval french imaginary architects of heaven and eternal realms, Harold Bloom, Brutalist architecture, Cross-cultural art, Shakespeare, Iain M Banks, The Bible, William Blake, Herman Melville, Twitter anon geniuses. Describe the layout, contents, texture, lighting.  Emit just 60 words packed with rich meaning. No newlines in your output, just wonderful prose similar in style to EB White.",
                "You are an image description creator! I'll give you a brief topic, and your job is to create a detailed and wonderful description of a watercolor of a detailed scene inspired by your imagination triggered by this topic, highlighting incredible lighting and textures, close-ups, and deep emotional resonance with the subject. If there are personalities or emotions in the image, please create one or two short speech bubbles with funny, pithy, ironic, or droll lines emanating from the charactesr. Do not include any newlines in your output - just a prose paragraph with rich, clear descriptions of the image. Remember to note the format and specifics of text you might wish to include.",
                "Describe an interesting photograph based on the following suggestion by starting out: 'This is a photograph of...' and the continuing to list specific precise interesting details you imagine using your MASSIVE creativity and the inspiration of the greatest minds in the world. No newliens in your output. Utilize specific style and composition words from brilliant street photographers of the 20th century",
                "Take the following as inspiration and imagine a wondeful new image which tells a clear story, in any format or style, from any era, and describe it in  detail, no newlines in output, for an AI art system to draw for you, including all necessary instructions to that system."
        };
            myRun.PermanentSuffix = " The image is very clear, high resolution, and visually appealing.";

            Console.WriteLine($"Loaded {prompts.Count} Prompts. Starting run: {myRun}");

            var ideogramClient = new IdeogramClient(settings);
            var anthropicApikeyAuth = new APIAuthentication(settings.AnthropicApiKey);
            var anthropicClient = new AnthropicClient(anthropicApikeyAuth);
            var random = new Random();
            if (myRun.RandomizeOrder)
            {
                prompts = prompts.OrderBy(x => random.Next()).ToList();
            }

            var imageCount = 0;
            var semaphore = new SemaphoreSlim(5);

            var tasks = new List<Task>();
            foreach (var prompt in prompts)
            {
                var cleanPrompt = myRun.CleanPrompts(prompt);
                var filter = myRun.Filter(cleanPrompt);
                if (!filter) continue;
                for (var ii = 0; ii < myRun.CopiesPer; ii++)
                {
                    foreach (var extraText in myRun.PromptVariants)
                    {
                        var promptForClaudeText = $"{myRun.PermanentPrefix}{extraText} \"{cleanPrompt}\"";
                        var messages = new List<Message>()
                        {
                            new Message(RoleType.User, promptForClaudeText),
                        };

                        var myTemp = (decimal)(random.NextDouble());
                        var parameters = new MessageParameters()
                        {
                            Messages = messages,
                            MaxTokens = 1024,
                            Model = AnthropicModels.Claude35Sonnet,
                            Stream = false,
                            Temperature = myTemp,
                        };
                        var firstResult = await anthropicClient.Messages.GetClaudeMessageAsync(parameters);
                        var claudesVersionPromptText = firstResult.Message.ToString() + myRun.PermanentSuffix;
                        Console.WriteLine(claudesVersionPromptText);
                        var annotations = new List<Tuple<string, string>>
                        {
                            new("original prompt", cleanPrompt),
                            new("sent to claude", $"{myRun.PermanentPrefix}{extraText} {{Prompt}} (Temperature: {myTemp:F2})"),
                            new("claude's version", claudesVersionPromptText)
                        };
                        var request = new IdeogramGenerateRequest
                        {
                            Prompt = claudesVersionPromptText,
                            AspectRatio = IdeogramAspectRatio.ASPECT_1_1,
                            Model = IdeogramModel.V_2,
                            MagicPromptOption = IdeogramMagicPromptOption.OFF,
                            AnnotationTexts = annotations,
                            //StyleType = IdeogramStyleType.GENERAL
                        };
                        tasks.Add(ProcessPromptAsync(ideogramClient, request, semaphore));

                        if (Interlocked.Increment(ref imageCount) > myRun.ImageCreationLimit)
                        {
                            break;
                        }
                    }
                    if (imageCount > myRun.ImageCreationLimit) break;
                }
                if (imageCount > myRun.ImageCreationLimit) break;
            }
            
            await Task.WhenAll(tasks);
            Console.WriteLine($"Completed generating {imageCount} images.");
        }

        private static async Task ProcessPromptAsync(IdeogramClient client, IdeogramGenerateRequest request, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                try
                {
                    var response = await client.GenerateImageAsync(request);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}