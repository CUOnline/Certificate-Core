using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Certificate.Web.Models
{
    public class CertificateViewModel
    {   
        public string QuizUrl { get; set; }
        public string Score { get; set; }
        public bool Pass { get; set; }
        public string FullName { get; set; }
        public string Title { get; set; }
        public string Time { get; set; }
    }
}
