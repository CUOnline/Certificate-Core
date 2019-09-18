using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Certificate.Web.Models
{
    public class AppSettings
    {
        public string BaseUrl { get; set; }
        public string CanvasBaseUrl { get; set; }
        public string CanvasApiKey { get; set; }
    }
}
