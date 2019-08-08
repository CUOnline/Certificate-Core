using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Certificate.Web.Models
{
    public class QuizSubmission
    {            
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("kept_score")]
        public long KeptScore { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("finished_at")]
        public string FinishedAt { get; set; }
    }
}
