using System;
using System.Collections.Generic;

namespace Dalle3
{
    public class IdeogramRun
    {
        public bool RandomizeOrder { get; set; }
        /// <summary>
        /// This will be run on the prompts to clean them, like to remove trailing/leading commas etc if you provide one. 
        /// The default is to take them as they are.
        /// </summary>
        public Func<string, string> CleanPrompts { get; set; } = (string x) => x;
        
        /// <summary>
        /// This can optionally filter in or out a prompt. If you override it, you just need a function which returns false for prompts you want to exclude, and true for ones you want to keep.
        /// </summary>
        public Func<string,bool> Filter { get; set; } = (string x) => true;
        
        /// <summary>
        /// The program will just bail out after making this many images.
        /// </summary>
        public int ImageCreationLimit { get; set; } = 50;
        
        /// <summary>
        /// For each fully rendered prompt how many times should we run it? default 1.
        /// </summary>
        public int CopiesPer { get; set; } = 1;

        /// <summary>
        /// if you want to add extra versions or suffixes to the prompt, you can add them here. Include at least "" to just do the prompt as given. 
        /// So like you might have "" here to run the prompt as is, and then also have extra instructions to make say a "titled" version like:
        /// "Please make up and include a prominent title at the top of the image".  Note: ideogram model 2 is not nearly as interested in adding
        /// text as earlier models were.
        /// </summary>
        public List<string> PromptVariants { get; set; }=  new List<string>() { ""};

        /// <summary>
        /// if provided, a permanent prefix to add to all prompts
        /// </summary>
        public string PermanentPrefix { get; set; } = "";
        public string PermanentSuffix { get; set; } = "";
    }
}