using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace IdeogramAPIClient
{
    public class IdeogramGenerateRequest
    {
        /// <summary>
        /// The prompt which is actually used on ideogram.
        /// </summary>
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

        /// <summary>
        /// If included, and if the client has been set up to save annotated versions of the images, here are keyvalue pairs to draw into the output.
        /// I use it to log how the prompt was prepared.
        /// </summary>
        public List<Tuple<string,string>> AnnotationTexts { get; set; } = new List<Tuple<string,string>>();
    }
}
