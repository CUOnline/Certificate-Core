using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Certificate.Web.Models
{
    public class GenerateConfigViewModel
    {
        public string CanvasBaseUrl { get; set; }
        public long CourseId { get; set; }
        public long QuizId { get; set; }
        public long ScoreRequirement { get; set; }
    }
}
