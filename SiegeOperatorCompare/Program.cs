﻿using ImageMagick;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SiegeOperatorCompare
{
    class Program
    {
        static HttpClient http = new HttpClient();
        static string cache = "cache/";
        static string minimums = "cache/weights.txt";
        static double globalMinimum = 0.15;

        //This is one of the best PC players, he took like 4 hours to get his rank when the season started
        // Using him because he is likely to have already played the new operators.
        static string Username = "J0hnny.AVANGAR"; 

        struct CompareResult
        {
            public string fileName;
            public string operatorName;
            public double weight;
            public double minimum;
        }

        static void Main(string[] args)
        {
            bool doCache = false;
            string input = null;
            string firstCheck = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    default:
                        Console.WriteLine("error: unkown argument " + args[i]);
                        return;

                    case "-c":
                    case "-cache":
                        doCache = true;
                        break;

                    case "-username":
                        Username = args[++i];
                        break;

                    case "-input":
                    case "-i":
                        input = args[++i];
                        break;

                    case "-cachedir":
                        cache = args[++i];
                        break;

                    case "-minimums":
                        minimums = args[++i];
                        break;

                    case "-first":
                        firstCheck = args[++i];
                        break;

                    case "-globalmin":
                        globalMinimum = double.Parse(args[++i]);
                        break;
                }
            }

            if (doCache)
                RecacheAsync().Wait();

            if (!string.IsNullOrEmpty(input))
            {
                CompareResult result = Compare(input, firstCheck).Result;
                Console.WriteLine(JsonConvert.SerializeObject(result));
            }
        }

        static async Task<CompareResult> Compare(string source, string firstOperator = null)
        {
            return await Task.Run(() =>
            {
                //Prepare the best match and all the weights
                CompareResult bestMatch = new CompareResult();
                WeightList weightList = new WeightList() { Minimum = globalMinimum };
                weightList.Load(minimums);

                using (var sourceIcon = new MagickImage(source))
                {
                    //We should check the first operator first. This will be our baseline to beat.
                    if (!string.IsNullOrEmpty(firstOperator))
                    {
                        var firstopResult = CheckOperator(sourceIcon, firstOperator);
                        if (firstopResult.weight >= weightList.GetWeight(firstOperator))
                            bestMatch = firstopResult;
                    }

                    //Iterate over every file and check the shrink
                    foreach (string file in Directory.EnumerateFiles(cache, "*.shrink.png"))
                    {
                        //Get the name and skip if we did them first
                        string operatorName = Path.GetFileNameWithoutExtension(file).Replace(".shrink", "").ToUpperInvariant();
                        if (operatorName == firstOperator) continue;

                        //Prepare their minimum
                        double minimum = weightList.GetWeight(operatorName);

                        //Get their result.
                        var result = CheckOperator(sourceIcon, operatorName);
                        if (result.weight > minimum && result.weight >= bestMatch.weight)
                        {
                            result.minimum = minimum;
                            bestMatch = result;
                        }
                    }
                }

                return bestMatch;
            });
        }

        private static CompareResult CheckOperator(MagickImage sourceIcon, string operatorName)
        {
            string filepath = Path.Combine(cache, operatorName + ".shrink.png");
            CompareResult result = new CompareResult()
            {
                operatorName = operatorName,
                fileName = filepath,
                weight = 0,
                minimum = 0
            };

            if (!File.Exists(filepath))
                return result;
            
            using (var operatorIcon = new MagickImage(filepath))
                result.weight = sourceIcon.Compare(operatorIcon, new ErrorMetric());
            
            return result;
        }

        /// <summary>
        /// Recaches all the icons
        /// </summary>
        /// <returns></returns>
        static async Task RecacheAsync()
        {

            //Make sure cache exists
            if (!Directory.Exists(cache))
                Directory.CreateDirectory(cache);

            WeightList weightList = new WeightList();

            if (File.Exists(minimums))
                weightList.Load(minimums);

            //Get all the operators
            JObject response = await GetOperators();
            foreach (var x in response)
            {
                //Download their image and crop their images.
                string name = x.Key;
                string url = x.Value["img"].ToString();
                string path = Path.Combine(cache, name + ".png");
                string pathShrink = Path.Combine(cache, name + ".shrink.png");

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

                //Add to the cache
                if (!weightList.Contains(name))
                    weightList.Add(name, 0.5);
            }

            //Save the file
            weightList.Save(minimums);
        }
        
        /// <summary>
        /// Downloads a list of operators
        /// </summary>
        /// <returns></returns>
        static async Task<JObject> GetOperators()
        {
            HttpResponseMessage response = await http.GetAsync("https://d.lu.je/siege/operators.php?username=" + Username);
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
        static async Task DownloadFileAsync(Uri requestUri, string filename)
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
