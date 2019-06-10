using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SiegeOperatorDigest
{
    class Digest
    {
        const string SIEGE_API = "https://d.lu.je/siege";
        static HttpClient http = new HttpClient();

        public string CaptureDirectory { get; set; } = "";
        public string Username { get; }
        public string AccessKey { get; }

        public bool DeleteImages { get; set; } = true;
        public int ScanRate { get; set; } = 2500;

        public string LastProcessedImage { get; private set; } = null;

        public Digest(string username, string accessKey)
        {
            Username = username;
            AccessKey = accessKey;
            CaptureDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/PursuitGG/Captures/Siege";
        }

        public async Task ContinouslyScan(CancellationToken cancelationToken)
        {
            Log("Starting Scan....");
            while (!cancelationToken.IsCancellationRequested)
            {
                try
                {
                    if (await Scan())
                        Log("Scanned a image!");
                }
                catch (Exception e)
                {
                    Log("Error occured while scanning image. " + e.Message);
                }

                await Task.Delay(ScanRate, cancelationToken);
            }
            Log("Left Scanning Loop");
        }

        /// <summary>
        /// Cleans all images from the previous directory
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CleanCaptureDirectory()
        {
            if (!Directory.Exists(CaptureDirectory))
                return false;

            return await Task.Run(() =>
            {
                Log("Cleaning Folders...");
                foreach (string directory in Directory.EnumerateDirectories(CaptureDirectory).OrderByDescending(q => q).Skip(1))
                {
                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch (Exception e)
                    {
                        Log("Failed to delete " + directory + ": " + e.Message);
                    }
                }
                return true;
            });
        }

        /// <summary>
        /// Scans the capture directory for the latest image
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Scan()
        {
            //20190610061557837
            if (!Directory.Exists(CaptureDirectory))
                return false;

            //Get all the folders, looking for the best one
            string image = await Task.Run(() =>
            {
                Log("Scanning Directories");
                string latest = Directory.EnumerateDirectories(CaptureDirectory).OrderByDescending(q => q).FirstOrDefault();
                if (string.IsNullOrEmpty(latest)) return null;

                //Get the latest image
                Log("Scanning Files");
                return Directory.EnumerateFiles(CaptureDirectory, "*.jpeg", SearchOption.AllDirectories).OrderByDescending(q => q).FirstOrDefault();                
            });

            //Make sure we havnt gotten this image before
            if (string.IsNullOrEmpty(image) || image.Equals(LastProcessedImage))
                return false;

            //Perform a digest
            await DigestFile(image);
            return true;
        }


        private async Task DigestFile(string file)
        {
            //Process the image
            Log("Processing image " + file);
            byte[] bytes = ImageProcessor.ProcessImage(file);

            //Upload the image
            Log("Uploading image " + file);
            var response = await UploadImageAsync($"{SIEGE_API}/evaluate.php?username={Username}&access_key={AccessKey}", bytes);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                Log("Success. " + content);
            }

            //Deleting image
            if (DeleteImages)
            {
                Log("Deleting image " + file);
                File.Delete(file);
            }
        }

        private async Task<HttpResponseMessage> UploadImageAsync(string url, byte[] ImageData, 
                    string fieldName = "image", string fileName = "image.png", string contentType = "image/png")
        {
            var requestContent = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(ImageData);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            requestContent.Add(imageContent, fieldName, fileName);
            var response = await http.PostAsync(url, requestContent);
            return response;
        }


        private void Log(string message)
        {
            Console.WriteLine("DIGEST: " + message);
        }
    }
}
