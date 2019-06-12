using ImageMagick;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SiegeOperatorCompare.Redis
{
    class CacheProcessor
    {
        static HttpClient http = new HttpClient();

        private IRedisClient redis;
        public Weights Weights { get; }
        public string CacheDirectory { get; }
        public string Profile { get; set; } = "J0hnny.AVANGAR";

        public CacheProcessor(IRedisClient redis, Weights weights, string cacheDirectory)
        {
            this.Weights = weights;
            this.redis = redis;
            this.CacheDirectory = cacheDirectory;
        }

        public async Task UpdateRedisCacheAsync(bool download = false)
        {
            //Make sure cache exists
            if (!Directory.Exists(CacheDirectory))
                Directory.CreateDirectory(CacheDirectory);

            //List of shrunk images that we will upload to redis
            string[] operators = null;

            //Download the operator files, otherwise just scan them
            if (download)   operators = await DownloadOperatorsAsync();
            else            operators = Directory.EnumerateFiles(CacheDirectory, "*.shrink.png", SearchOption.TopDirectoryOnly).ToArray();

            //Upload all the files to redis
            foreach(var op in operators)
            {
                //Get the bytes and the op name
                byte[] img = File.ReadAllBytes(op);
                string opname = Path.GetFileNameWithoutExtension(op).Replace(".shrink", "");

                //Upload to redis
                Console.WriteLine("Redis Cache: {0}", opname);

                string bytestr = Convert.ToBase64String(img);
                redis.SetEntryInHash("siege:cache", opname, bytestr);

                //redis.Set($"siege:cache:{opname}", img);


                //Add the weights if required
                if (!Weights.Contains(opname))
                    Weights.Add(opname);
            }
        }

        /// <summary>
        /// Recaches all the icons
        /// </summary>
        /// <returns></returns>
        public async Task<string[]> DownloadOperatorsAsync()
        {
            //Make sure cache exists
            if (!Directory.Exists(CacheDirectory))
                Directory.CreateDirectory(CacheDirectory);


            //Get all the operators
            int index = 0;
            JObject response = await GetOperators();
            string[] operators = new string[response.Count];
            foreach (var x in response)
            {
                //Download their image and crop their images.
                string name = x.Key;
                string url = x.Value["img"].ToString();
                string path = Path.Combine(CacheDirectory, name + ".png");
                string pathShrink = Path.Combine(CacheDirectory, name + ".shrink.png");

                operators[index++] = pathShrink;

                //Download file
                Console.WriteLine("Downloading and Cropping {0}", name);
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

            return operators;
        }

        /// <summary>
        /// Downloads a list of operators
        /// </summary>
        /// <returns></returns>
        private async Task<JObject> GetOperators()
        {
            HttpResponseMessage response = await http.GetAsync($"https://d.lu.je/siege/operators.php?username={Profile}");
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
        private async Task DownloadFileAsync(Uri requestUri, string filename)
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

    }
}
