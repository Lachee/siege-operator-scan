using ImageMagick;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SiegeOpDetector
{
    class Program
    {
        static HttpClient http = new HttpClient();
        const string CacheFolder = "cache/";
        const string OutputImage = "tmp_frame.png";
        static string Username = "KommadantKlink";

        static void Main(string[] args)
        {
            bool recache = false;
            bool cleanup = false;
            string image = null;
            string video = null;
            string digest = null;
            bool cleanup_digest = false;
            string ffmpeg = "Resources/ffmpeg.exe";
            double min = 0.5;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-recache":
                        recache = true;
                        break;

                    case "-cleanup":
                        cleanup = true;
                        break;

                    case "-image":
                        image = args[++i];
                        break;

                    case "-video":
                        video = args[++i];
                        break;
                        
                    case "-ffmpeg":
                        ffmpeg = args[++i];
                        break;

                    case "-digest":
                        digest = args[++i];
                        break;

                    case "-cdigest":
                        cleanup_digest = true;
                        break;

                    case "-username":
                        Username = args[++i];
                        break;

                    case "-minimum":
                        if (!double.TryParse(args[++i], out min)) Console.WriteLine("error: failed to parse " + min + " to double!");
                        break;

                }
            }

            if (recache)
            {
                //Recache everything
                Console.WriteLine("info: Recaching Everything...");
                CacheOperators().Wait();
                Console.WriteLine("info: Cache Complete. Aborting!");
                return;
            }

            if (digest != null)
            {
                Console.WriteLine("info: Waiting for digest. Press anykey to abort.");
                while (!Console.KeyAvailable)
                {
                    while (!File.Exists(digest) && !Console.KeyAvailable)
                        Thread.Sleep(10000);

                    string content = File.ReadAllText(digest);
                    if (!string.IsNullOrEmpty(content))
                    {
                        if (!File.Exists(content))
                        {
                            Console.WriteLine("error: file does not exist: '{0}'", content);
                        }
                        else if (video != content)
                        {
                            video = content;

                            Console.WriteLine("info: digesting video " + digest);
                            image = ProcessVideo(ffmpeg, video);

                            if (!File.Exists(image))
                            {
                                Console.WriteLine("error: failed to find processed image '{0}'", image);
                            }
                            else
                            { 
                                Console.WriteLine("info: uploading Frame...");
                                byte[] frame = File.ReadAllBytes(image);
                                UploadImageAsync($"https://d.lu.je/siege/evaluate.php?username={Username}&access_key=Hfkd67ASfbasf", frame).Wait();
                            }

                            Console.WriteLine("info: cleaning up...");
                            if (cleanup_digest) File.Delete(digest);
                            if (cleanup && video == null)
                            {
                                Console.WriteLine("Deleting Video...");
                                File.Delete(video);
                                File.Delete(OutputImage);
                            }
                        }
                    }

                    Console.Write(".");
                    Thread.Sleep(2000);
                }

                Console.WriteLine("info: digest aborted");
                return;
            }


            if (video != null)
            {
                if (!File.Exists(video))
                {
                    Console.WriteLine("error: cannot find the video " + video);
                    return;
                }

                //Process the video and get the image url
                Console.WriteLine("info: processing video");
                image = ProcessVideo(ffmpeg, video);
            }

            if (image != null)
            {
                if (!File.Exists(image))
                {
                    Console.WriteLine("error: cannot find the image " + image);
                    return;
                }

                Console.WriteLine("info: processing best operator");
                string best = BestMatch(image, min).Result;
                string bestOperator = Path.GetFileNameWithoutExtension(best).Replace(".shrink", "");
                Console.WriteLine("ok: " + bestOperator);
            }

            if (cleanup)
            {
                Console.WriteLine("info: cleaning Up...");
                if (video == null) File.Delete(video);
            }
        }

        static string ProcessVideo(string ffmpeg, string video)
        {
            if (!File.Exists(video))
                return null;

            if (File.Exists(OutputImage))
                File.Delete(OutputImage);

            var process = Process.Start(ffmpeg, $"-y -i \"{video}\" -vf \"crop=22:22:500:50,select=eq(n\\,150)\" -vframes 1 \"{OutputImage}\"");
            process.WaitForExit();
            return OutputImage;
        }
        

        /// <summary>
        /// Finds the file with the best match out of the .shrink.png images.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        static async Task<string> BestMatch(string source, double minMetric = 0.70)
        {
            return await Task.Run(() =>
            {
                double bestMatch = minMetric;
                string bestFile = "";

                using (var imgSource = new MagickImage(source))
                {
                    foreach (string file in Directory.EnumerateFiles(CacheFolder, "*.shrink.png"))
                    {
                        using (var imgShrink = new MagickImage(file))
                        {
                            double diff = imgSource.Compare(imgShrink, new ErrorMetric());
                            if (diff > bestMatch)
                            {
                                bestMatch = diff;
                                bestFile = file;
                            }
                        }
                    }
                }

                Console.WriteLine("ok: {0} - {1}", bestMatch, bestFile);
                return bestFile;
            });
        }

        /// <summary>
        /// Downloads the icon for every operator, then shrinks them and stores both images into the file cache.
        /// </summary>
        /// <returns></returns>
        static async Task CacheOperators()
        {
            //Make sure cache exists
            if (!Directory.Exists(CacheFolder))
                Directory.CreateDirectory(CacheFolder);

            //Get all the operators
            JObject response = await GetOperators();            
            foreach (var x in response)
            {
                //Download their image and crop their images.
                string name         = x.Key;
                string url          = x.Value["img"].ToString();
                string path         = Path.Combine(CacheFolder, name + ".png");
                string pathShrink   = Path.Combine(CacheFolder, name + ".shrink.png");

                Console.WriteLine("Processing " + name);

                //Download file
                await DownloadFileAsync(new Uri(url), path);

                //Shrink the file
                using (var source = new MagickImage(path))
                {
                    source.Crop(new MagickGeometry(19, 21, 89, 89));
                    source.Scale(22, 22);
                    source.RePage();
                    source.Write(pathShrink);
                }
            }
        }

        /// <summary>
        /// Downloads a list of operators
        /// </summary>
        /// <returns></returns>
        static async Task<JObject> GetOperators()
        {
            HttpResponseMessage response = await http.GetAsync("https://d.lu.je/siege/operators.php?username=KommadantKlink");
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                return JObject.Parse(content);
            }

            return null;
        }

        /// <summary>
        /// Downloads a file
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static async Task DownloadFileAsync(Uri requestUri, string filename)
        {
            if (filename == null)
                throw new ArgumentNullException("filename");

            using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                using (Stream contentStream = await (await http.SendAsync(request)).Content.ReadAsStreamAsync(), stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await contentStream.CopyToAsync(stream);
                }
            }
        }

        public static async Task<HttpResponseMessage> UploadImageAsync(string url, byte[] ImageData)
        {
            var requestContent = new MultipartFormDataContent();
            //    here you can specify boundary if you need---^
            var imageContent = new ByteArrayContent(ImageData);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

            requestContent.Add(imageContent, "image", "image.png");
            var response =  await http.PostAsync(url, requestContent);
            if (response.IsSuccessStatusCode)
            {
                string txt = await response.Content.ReadAsStringAsync();
                Console.WriteLine(txt);
            }

            return response;
        }
    }
}