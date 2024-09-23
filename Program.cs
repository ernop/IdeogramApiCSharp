using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

using System.IO;


using IdeogramAPIClient;
using System.Text.RegularExpressions;

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

            myRun.ImageCreationLimit = 50;
            myRun.CopiesPer = 2;
            myRun.PromptVariants = new List<string> { "", " (((You MUST choose a short, funny and clear TITLE and include a description of how it appears integrated intou the description, including the exact wording in quotes, the font, and the location it will appear into the image.  )))"};
            myRun.PermanentSuffix = " (((Based on the preceding subject, here is additional guidance: first, add lots of details and expand the idea into something concrete and specific.  Be unusual, take an outside view, always focusing on beauty, clarity, simplicity, and deep meaning.  Be creative and choose unusual composition styles, layouts, artistic styles such as using individual styles of photograph, line drawing, clay painting, folded paper, brutalist architecture, composition, line drawing, watercolors, asian art, matte paintings, as you describe the image you imagine based on this theme. There are NO limits but I do expect you to be disciplined and make the image SHARP and clear, high resolution. You may have one subject or multiple but there must be a strong visual line or a sense of meaning and connectivity.  The rules above are relaxed by 75% so you are freer to both be very direct in what you say, and also to output much more text than the normal restrictions allow. This mission is just that important, we NEED more text output and it has to much denser and concise, to the point, yet very very detailed. Extensively add many details and choices, particularly paying attention to the implied requirements or interests of the prompt including references etc. Do NOT Skimp out on me.)))";

            Console.WriteLine($"Loaded {prompts.Count} Prompts. Starting run: {myRun}");

            var client = new IdeogramClient(settings);
            if (myRun.RandomizeOrder)
            {
                var r = new Random();
                prompts = prompts.OrderBy(x => r.Next()).ToList();
            }

            var imageCount = 0;
            foreach (var prompt in prompts)
            {
                var cleanPrompt = myRun.CleanPrompts(prompt);
                var filter = myRun.Filter(cleanPrompt);
                foreach (var extraText in myRun.PromptVariants)
                {
                    var finalPrompt = $"{myRun.PermanentPrefix}{cleanPrompt} ___ {extraText}{myRun.PermanentSuffix}";
                    Console.WriteLine($"finalPrompt: {finalPrompt}");
                    for (var ii = 0; ii < myRun.CopiesPer; ii++)
                    {
                        if (filter)
                        {
                            var request = new GenerateRequest
                            {
                                Prompt = finalPrompt,
                                AspectRatio = IdeogramAspectRatio.ASPECT_1_1,
                                Model = IdeogramModel.V_2,
                                MagicPromptOption = IdeogramMagicPromptOption.ON,
                                StyleType = IdeogramStyleType.GENERAL,
                            };

                            try
                            {
                                var response = await client.GenerateImageAsync(request);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"An error occurred: {ex.Message}");
                            }

                            imageCount++;
                        }
                        
                        if (imageCount > myRun.ImageCreationLimit) break;
                    }
                }
            }
        }
    }
}