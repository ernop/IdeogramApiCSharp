using System;

using Newtonsoft.Json;

namespace IdeogramAPIClient
{
    public class GenerateRequest
    {
        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        private IdeogramAspectRatio? _aspectRatio;
        private IdeogramResolution? _resolution;

        [JsonProperty("aspect_ratio")]
        public IdeogramAspectRatio? AspectRatio
        {
            get => _aspectRatio;
            set
            {
                if (value.HasValue && _resolution.HasValue)
                    throw new InvalidOperationException("AspectRatio and Resolution cannot be used together.");
                _aspectRatio = value;
            }
        }

        [JsonProperty("resolution")]
        public IdeogramResolution? Resolution
        {
            get => _resolution;
            set
            {
                if (value.HasValue && _aspectRatio.HasValue)
                    throw new InvalidOperationException("AspectRatio and Resolution cannot be used together.");
                _resolution = value;
            }
        }

        [JsonProperty("model")]
        public IdeogramModel? Model { get; set; }

        [JsonProperty("magic_prompt_option")]
        public IdeogramMagicPromptOption? MagicPromptOption { get; set; }

        [JsonProperty("seed")]
        public int? Seed { get; set; }

        [JsonProperty("style_type")]
        public IdeogramStyleType? StyleType { get; set; }

        [JsonProperty("negative_prompt")]
        public string NegativePrompt { get; set; }
    }
}
