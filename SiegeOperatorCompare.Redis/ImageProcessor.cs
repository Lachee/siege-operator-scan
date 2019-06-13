using ImageMagick;
using ServiceStack.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SiegeOperatorCompare.Redis
{
    class ImageProcessor : IDisposable
    {
        public const string SESSION_ROOT = "siege:sessions";
        public const string PENDING_KEY = SESSION_ROOT + ":pending";

        private IRedisClientsManager manager;
        private IRedisClient redis;
        public TimeSpan Timeout { get; }
        public TimeSpan PlaytimeExpirey { get; set;  }
        public Weights Weights { get; }


        public ImageProcessor(IRedisClientsManager manager, TimeSpan timeout, Weights weights)
        {
            this.manager = manager;
            this.Timeout = timeout;
            this.Weights = weights;
            this.PlaytimeExpirey = TimeSpan.FromSeconds(30);
        }

        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                this.redis = manager.GetClient();

                while (!cancellationToken.IsCancellationRequested)
                {
                    //Get the name that has been queued, and process it
                    string username = redis.BlockingPopItemFromList(PENDING_KEY, Timeout);
                    if (string.IsNullOrEmpty(username)) continue;

                    //Process the session
                    if (!cancellationToken.IsCancellationRequested)
                        ProcessSession(username);
                }
            }, cancellationToken);
        }

        public void ProcessSession(string username)
        {
            //Prepare some data for the user
            Console.WriteLine("Processing Username: {0}", username);
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            byte[] image = redis.Get<byte[]>($"siege:{username}:icon");
            if (image == null || image.Length == 0) return;

            //The best operator
            Operator bestOperator = null;

            //Get the users current operator and image
            var currentOperator = GetCurrentOperator(username);
            Console.WriteLine("Current Operator: {0}", currentOperator?.Name);

            //Find the best operator
            using (var sourceImage = new MagickImage(image))
            {
                //Get all the other operators
                var allOperators = redis.GetAllEntriesFromHash("siege:cache");

                //Check the current operator
                if (currentOperator != null && allOperators.TryGetValue(currentOperator.Name, out var cacheByteString))
                {
                    bestOperator = currentOperator;
                    bestOperator.Match = CompareSourceWithCache(sourceImage, cacheByteString);

                    //Make sure the match is actually valid.
                    if (bestOperator.Match < bestOperator.MinimumMatch)
                        bestOperator = null;
                }

                //If we already have a valid good operator, then skip the checks,
                //  otherise iterate over every value, and check if its better
                if (currentOperator == null)
                {
                    foreach (var keypair in allOperators)
                    {
                        double minimum = Weights.GetWeight(keypair.Key);
                        double match = CompareSourceWithCache(sourceImage, keypair.Value);

                        if (match >= minimum && (bestOperator == null || bestOperator.Match < match))
                            bestOperator = new Operator(keypair.Key, minimum) { Match = match };
                    }
                }
            }

            //Tada we found someone, maybe
            if (bestOperator != null)
            {
                Console.WriteLine("Best Operator {0} at {1}% (min: {2})", bestOperator.Name, bestOperator.Match * 100, bestOperator.MinimumMatch);
                SetCurrentOperator(username, bestOperator);
            }

            Console.WriteLine("Completed in {0}ms", stopwatch.ElapsedMilliseconds);
            Console.WriteLine("=============================");
        }

        private double CompareSourceWithCache(MagickImage sourceImage, string cacheByteString)
        {
            byte[] cacheBytes = Convert.FromBase64String(cacheByteString);
            using (MagickImage cacheImage = new MagickImage(cacheBytes))
                return  sourceImage.Compare(cacheImage, new ErrorMetric());
        }

        public Operator GetCurrentOperator(string username)
        {
            string opname = redis.Get<string>($"siege:{username}:playing");
            if (opname == null) return null;
            return new Operator(opname, Weights.GetWeight(opname));
        }

        public void SetCurrentOperator(string username, Operator op)
        {
            redis.SetValue($"siege:{username}:playing", op.Name, PlaytimeExpirey);

        }


        public void Dispose()
        {
            if (redis != null)
            {
                redis.Dispose();
                redis = null;
            }
        }
    }
}
