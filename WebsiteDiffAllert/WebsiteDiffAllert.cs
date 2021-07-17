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
            var connectionString = Environment.GetEnvironmentVariable("WEBSITE_STORE_CONNECTION_STRING");
            var websiteUrl = Environment.GetEnvironmentVariable("WEBSITE_URL");
            var webhook = Environment.GetEnvironmentVariable("WEBHOOK_URL");
            var blobContainerName = Environment.GetEnvironmentVariable("WEBSITE_STORE_CONTAINER");
            var fileName = "website.html";
            var currentFile = Path.Combine(System.IO.Path.GetTempPath(), fileName);
            var oldFile = Path.Combine(System.IO.Path.GetTempPath(), "website-old.html");

            using (WebClient client = new WebClient()) {

                log.LogInformation("Downloading current version of webiste " + websiteUrl);
                client.DownloadFile(websiteUrl, currentFile);

                log.LogInformation("Initializing blob service client");
                BlobServiceClient serviceClient = new BlobServiceClient(connectionString);

                log.LogInformation("Initializing blob container client for container " + blobContainerName);
                BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(blobContainerName);

                log.LogInformation("Initializing blob client for " + fileName);
                BlobClient blobClient = containerClient.GetBlobClient(fileName);

                log.LogInformation("Checking if blob " + fileName + " already exists");
                if (blobClient.Exists())
                {
                    await blobClient.DownloadToAsync(oldFile);

                    log.LogInformation("Download website from blob storage");

                    string currentBody = Body(File.ReadAllText(currentFile));
                    string oldBody = Body(File.ReadAllText(oldFile));

                    if (!currentBody.Equals(oldBody))
                    {
                        log.LogInformation("Identified website change. Triggering webhook to fire alert...");
                        await TriggerWebhook(webhook, websiteUrl, log);
                    }
                    else
                    {
                        log.LogInformation("The website did not change since the last check");
                    }

                    log.LogInformation("Deleting old version of the website");
                    await blobClient.DeleteAsync();
                }
                else {
                    log.LogInformation("Skipping comparison because no older version was found in the blob container");
                }

                log.LogInformation("Uploading new version of the website");
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
