using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Certificate.Web.Controllers;
using Newtonsoft.Json;

namespace Certificate.Web.Models
{
    public class QuizSubmissionsResponse
    {
        [JsonProperty("quiz_submissions")] 
        public List<QuizSubmission> QuizSubmissions { get; set; }
    }
}
