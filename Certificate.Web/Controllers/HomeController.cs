using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Certificate.Web.Models;
using Microsoft.Extensions.Options;
using System.Xml;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json;
using Xfinium.Pdf;
using Xfinium.Pdf.Graphics;
using Xfinium.Pdf.LogicalStructure;

namespace Certificate.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppSettings appSettings;
        private readonly IHostingEnvironment environment;
        private readonly HttpClient canvasClient;

        public HomeController(IOptions<AppSettings> appSettings, IHostingEnvironment environment, IHttpClientFactory httpClientFactory)
        {
            this.appSettings = appSettings.Value;
            this.environment = environment;
            this.canvasClient = httpClientFactory.CreateClient(HttpClientNames.CanvasApiClient);
        }

        [HttpPost]
        public async Task<IActionResult> Certificate()
        {
            var ltiParams = ParseLtiParams(HttpContext.Request.Form);
            var userFullName = ltiParams["lis_person_name_full"];
            var userId = ltiParams["custom_canvas_user_id"];
            var scoreRequirement = long.Parse(ltiParams["custom_score_req"]);
            var canvasCourseId = ltiParams["custom_canvas_course_id"];
            var quizId = ltiParams["custom_quiz_id"];
            var contextTitle = ltiParams["context_title"];

            var submissions = await GetSubmissions(canvasCourseId, quizId);
            var userPassedSubmission = submissions.FirstOrDefault(x => x.UserId == userId && x.KeptScore >= scoreRequirement);

            var model = new CertificateViewModel();

            // If user passed quiz
            if (userPassedSubmission != null)
            {
                model.Pass = true;
                var datePassed = !string.IsNullOrWhiteSpace(userPassedSubmission.FinishedAt)
                    ? DateTime.Parse(userPassedSubmission.FinishedAt)
                    : DateTime.Parse(userPassedSubmission.StartedAt);

                model.FullName = userFullName;
                model.Title = contextTitle;
                model.Time = datePassed.ToShortDateString();
            }

            //// This is required for the LTI app to respond without a 403 error.
            Response.Headers.Add("X-Frame-Options", $"ALLOW-FROM {appSettings.CanvasBaseUrl}");
            return View(model);
        }
        private async Task<List<QuizSubmission>> GetSubmissions(string canvasCourseId, string quizId)
        {
            var submissions = await GetSubmissions($"api/v1/courses/{canvasCourseId}/quizzes/{quizId}/submissions?per_page=100");
            return submissions;
        }

        private async Task<List<QuizSubmission>> GetSubmissions(string url)
        {
            var response = await canvasClient.GetAsync(url);

            var submissions = new List<QuizSubmission>();
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.Contains("Link"))
                {
                    response.Headers.TryGetValues("Link", out IEnumerable<string> links);

                    var nextLink = GetNextLink(links.First());

                    if (!string.IsNullOrWhiteSpace(nextLink))
                        submissions.AddRange(await GetSubmissions(nextLink));
                }

                var data = await response.Content.ReadAsStringAsync();
                var submissionResponse = JsonConvert.DeserializeObject<QuizSubmissionsResponse>(data);
                submissions.AddRange(submissionResponse.QuizSubmissions);
            }

            return submissions;
        }

        private string GetNextLink(string link)
        {
            var links = link.Split(',');
            var nextLinkItem = links.FirstOrDefault(x => x.Contains("rel=\"next\""));

            if (nextLinkItem == null)
                return "";

            var nextLink = nextLinkItem.Split(',')[0];

            return nextLink.Replace($"<{appSettings.CanvasBaseUrl}", "").Replace(">", "");
        }

        public IActionResult ConfigError()
        {
            return View();
        }

        public IActionResult GenerateConfig()
        {
            var model = new GenerateConfigViewModel();
            model.CanvasBaseUrl = appSettings.CanvasBaseUrl;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateConfig(GenerateConfigViewModel model)
        {
            try
            {
                var quizExists = await canvasClient.GetStringAsync($"api/v1/courses/{model.CourseId}/quizzes/{model.QuizId}");
                return RedirectToAction(nameof(Config), new { courseId = model.CourseId, quizId = model.QuizId, scoreReq = model.ScoreRequirement });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("QuizDNE", "Quiz with specified ID not found. Note that it may take up to 48" +
                                                    " hours for a newly created quiz to show up in the database.");
            }

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [FormatFilter]
        public XmlDocument Config(string courseId, string quizId, string scoreReq)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load($"{environment.WebRootPath}/content/lti_config.xml");

            doc.GetElementsByTagName("blti:launch_url")[0].InnerText = $"{appSettings.BaseUrl}Home/Certificate";
            GetNodeByName(doc.ChildNodes, "course_id").InnerText = courseId;
            GetNodeByName(doc.ChildNodes, "quiz_id").InnerText = quizId;
            GetNodeByName(doc.ChildNodes, "score_req").InnerText = scoreReq;
            GetNodeByName(doc.ChildNodes, "domain").InnerText = appSettings.BaseUrl;
            GetNodeByName(doc.ChildNodes, "url").InnerText = $"{appSettings.BaseUrl}Home/Certificate";

            HttpContext.Response.ContentType = "application/xml";
            return doc;
        }

        private XmlNode GetNodeByName(XmlNodeList nodes, string name)
        {
            foreach (XmlNode node in nodes)
            {
                if (node.HasChildNodes)
                {
                    var xmlNode = GetNodeByName(node.ChildNodes, name);
                    if (xmlNode != null)
                    {
                        return xmlNode;
                    }
                }

                if (node.Attributes != null)
                {
                    XmlAttributeCollection attrs = node.Attributes;
                    foreach (XmlAttribute attr in attrs)
                    {
                        if (attr.Name == "name" && attr.Value == name)
                        {
                            return node;
                        }
                    }
                }
            }

            return null;
        }

        public IActionResult DownloadCertificate(string fullName, string title, string time)
        {
            var document = GenerateCertificatePdf(fullName, title, DateTime.Parse(time));

            var stream = new MemoryStream();
            document.Save(stream);
            stream.Position = 0;

            Response.Headers.Append("Content-Disposition", "attachment; filename=certificate.pdf");
            return File(stream, "application/pdf");
        }

        private PdfFixedDocument GenerateCertificatePdf(string userFullName, string title, DateTime quizTime)
        {
            PdfFixedDocument document = new PdfFixedDocument();
            PdfStandardFont helvetica = new PdfStandardFont(PdfStandardFontFace.Helvetica, 16);
            PdfBrush blackBrush = new PdfBrush();
            PdfStringLayoutOptions slo = new PdfStringLayoutOptions();
            slo.HorizontalAlign = PdfStringHorizontalAlign.Center;
            slo.VerticalAlign = PdfStringVerticalAlign.Middle;

            helvetica.Size = 30;
            PdfStringAppearanceOptions sao = new PdfStringAppearanceOptions();
            sao.Brush = blackBrush;
            sao.Font = helvetica;

            PdfPage page = document.Pages.Add();
            page.Rotation = 90;

            // Add border
            PdfPen blackPen = new PdfPen(new PdfRgbColor(0, 0, 0), 48);
            page.Graphics.DrawRectangle(blackPen, 0, 0, page.Width, page.Height);

            var verticalSpacing = 45;
            // Add Text
            slo.X = 395;
            slo.Y = 70;
            page.Graphics.DrawString("This document certifies that", sao, slo);

            slo.Y += verticalSpacing;
            page.Graphics.DrawString(userFullName, sao, slo);

            slo.Y += verticalSpacing;
            page.Graphics.DrawString("has completed requirements for", sao, slo);

            slo.Y += verticalSpacing;
            page.Graphics.DrawString(title, sao, slo);

            slo.Y += verticalSpacing;
            page.Graphics.DrawString("on", sao, slo);

            slo.Y += verticalSpacing;
            page.Graphics.DrawString(quizTime.ToShortDateString(), sao, slo);

            DrawImages(page);

            return document;
        }

        private void DrawImages(PdfPage page)
        {
            WebClient client = new WebClient();

            using (var stream = client.OpenRead($"{environment.WebRootPath}\\images\\logo.png"))
            {
                PdfPngImage image = new PdfPngImage(stream);
                page.Graphics.DrawImage(image, 115, 335, 562, 250);
                page.Graphics.CompressAndClose();
            }
        }

        private bool IsValidLtiRequest(HttpRequest request, Dictionary<string, string> ltiParams)
        {
            // Write later
            return true;
        }

        private Dictionary<string, string> ParseLtiParams(IFormCollection form)
        {
            Dictionary<string, string> LtiParams = new Dictionary<string, string>();

            if (form != null && form.Count > 0)
            {
                foreach (var pair in form)
                {
                    LtiParams.Add(pair.Key, pair.Value);
                }
            }

            return LtiParams;
        }

    }
}
