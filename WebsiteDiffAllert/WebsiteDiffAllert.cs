using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace WebsiteDiffAllert
{
    public static class WebsiteDiffAllert
    {
        [FunctionName("WebsiteDiffAllert")]
        public static async Task RunAsync([TimerTrigger("*/15 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
            var websiteUrl = Environment.GetEnvironmentVariable("WEBSITE_URL");
            var webhook = Environment.GetEnvironmentVariable("ALERT_WEBHOOK");
            var currentFile = "website.html";
            var oldFile = "website-old.html";

            using (WebClient client = new WebClient()) {

                client.DownloadFile("https://red.mediamarkt.at/playstation-5.html", currentFile);

                BlobServiceClient serviceClient = new BlobServiceClient(connectionString);

                BlobContainerClient containerClient = serviceClient.GetBlobContainerClient("htmlcontainer");

                BlobClient blobClient = containerClient.GetBlobClient(currentFile);

                if (blobClient.Exists()) {


                    await blobClient.DownloadToAsync(oldFile);

                    log.LogDebug("Download website from blob storage");

                    string currentBody = Body(File.ReadAllText(currentFile));
                    string oldBody = Body(File.ReadAllText(oldFile));

                    if (!currentBody.Equals(oldBody)) {
                        log.LogInformation("Identified website change");

                        await TriggerWebhook(webhook, websiteUrl, log);
                    }
                    else {
                        log.LogDebug("The website did not change");
                    }
                }

                await blobClient.DeleteAsync();
                await blobClient.UploadAsync(currentFile);
            }
        }

        public static string Body(String html) {
            int start = html.IndexOf("<body>");
            return html.Substring(start, html.Length - start);
        }

        public async static Task TriggerWebhook(string webhookUrl, string websiteUrl, ILogger log) {
            var data = Encoding.UTF8.GetBytes("{\"text\":\"The website " + websiteUrl + " changed since the last check!!!\"}");
            WebRequest request = WebRequest.Create(webhookUrl);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = data.Length;

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            try
            {
                WebResponse response = await request.GetResponseAsync();
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseContent = reader.ReadToEnd();
                    log.LogInformation("Webhook response: " + responseContent);
                }
            }
            catch (WebException webException)
            {
                if (webException.Response != null)
                {
                    using (StreamReader reader = new StreamReader(webException.Response.GetResponseStream()))
                    {
                        string responseContent = reader.ReadToEnd();
                        log.LogInformation("Webhook response: " + responseContent);
                    }
                }
            }
        } 
    }
}
