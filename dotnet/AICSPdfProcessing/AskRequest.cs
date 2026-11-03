using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AICSPdfProcessing
{
    public class AskRequest
    {
        [Required]
        [MinLength(1)]
        [Description("Question to answer from the ingested documents")]
        public string Prompt { get; set; } = string.Empty;
        [Range(1, 10)]
        [DefaultValue(4)]
        [Description("Number of document chunks to use")]
        public int TopK { get; set; } = 4;
    }
}
