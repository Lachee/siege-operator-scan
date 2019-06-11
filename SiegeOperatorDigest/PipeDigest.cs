using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SiegeOperatorDigest
{
    class PipeDigest
    {
        const string SIEGE_API = "https://d.lu.je/siege";
        static HttpClient http = new HttpClient();

        public string Username { get; }
        public string AccessKey { get; }
        public int UploadRate { get; set; } = 10;

        private byte[] _buffer;
        private int _bytesRead = 0;

        private DateTime _lastCheck;

        //A timer to tell us when to upload again
        public PipeDigest(string username, string accessKey, int bufferSize = 1280 * 720)
        {
            Username = username;
            AccessKey = accessKey;
            _lastCheck = DateTime.MinValue;
            _buffer = new byte[bufferSize];
        }

        public async Task ContinouslyScan(CancellationToken cancelationToken)
        {
            Log("Starting Scan....");
            using (NamedPipeClientStream client = new NamedPipeClientStream(".", "obsframes", PipeDirection.In))
            {
                while (!cancelationToken.IsCancellationRequested)
                {
                    // Connect to the pipe or wait until the pipe is available.
                    Log("Attempting to connect to pipe...");
                    client.Connect();

                    Log("Connected to pipe, performing reads.");
                    do
                    {
                        //Read the bytes asyncronously. We supply a cancelation token incase we cancel in the meantime
                        _bytesRead = await client.ReadAsync(_buffer, 0, _buffer.Length, cancelationToken);

                        //We have read bytes, but it may have been because of a cancelation. We should only process if we are able too
                        if (!cancelationToken.IsCancellationRequested)
                            await DigestBytes(_buffer, _bytesRead);

                        //WE will loop back until we have recieved 0 bytes or been requested to abort
                    }  while (_bytesRead > 0 && !cancelationToken.IsCancellationRequested);
                }
            }
            Log("Left Scanning Loop");
        }


        private async Task DigestBytes(byte[] bytes, int count)
        {

            //Have we tested in a while?
            if ((DateTime.Now - _lastCheck).TotalSeconds < UploadRate)
                return;

            //Process the image
            byte[] opicon = ImageProcessor.ProcessImage(bytes, 0, count);

            //Upload the image
            //Log("Uploading image " + file);
            Log("Uploading " + opicon.Length + "b from " + count + "b");
            var response = await UploadImageAsync($"{SIEGE_API}/evaluate.php?username={Username}&access_key={AccessKey}", opicon);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                try
                {
                    var result = JsonConvert.DeserializeObject<CompareResult>(content);
                    Log("Operator\t\t" + result.operatorName);
                    Log("Weight\t\t" + result.weight);
                    Log("Min\t\t" + result.minimum);
                    Log("======================");
                    _lastCheck = DateTime.Now;
                }
                catch (Exception e)
                {
                    Log("error: " + e.Message);
                    Log("- " + content);
                }
            }
        }

        struct CompareResult
        {
            public string fileName;
            public string operatorName;
            public double weight;
            public double minimum;
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
