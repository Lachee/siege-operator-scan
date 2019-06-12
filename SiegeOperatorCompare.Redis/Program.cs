using ServiceStack.Redis;
using System;
using System.Linq;
using System.Threading;

namespace SiegeOperatorCompare.Redis
{
    class Program
    {
        static string weightPath = "cache/weights.txt";
        static string cachePath = "cache/";
        static bool downloadCache = false;

        static PooledRedisClientManager redisManager;

        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    default:
                        Console.WriteLine("Unkown argument {0}", args[i]);
                        break;

                    case "-cachedir":
                        cachePath = args[++i];
                        break;

                    case "-dl":
                    case "-download":
                        downloadCache = true;
                        break;

                    case "-minimums":
                    case "-weights":
                    case "-w":
                        weightPath = args[++i];
                        break;
                }

            }


            using(redisManager = new PooledRedisClientManager())
            {
                //Load the weights
                Weights weights = new Weights();
                weights.Load(weightPath);

                //Prepare and initialize the cache
                CacheProcessor cacheProcessor = new CacheProcessor(redisManager.GetClient(), weights, cachePath);
                cacheProcessor.UpdateRedisCacheAsync(downloadCache).Wait();

                //Save the weights again
                weights.Save(weightPath);

                //Prepare the processor and start the main ;
                var tokenSource = new CancellationTokenSource();
                var processor   = new ImageProcessor(redisManager, TimeSpan.FromSeconds(10), weights);
                var listenTask  = processor.ListenAsync(tokenSource.Token);

                //get our own client and do our own loop
                HandleCli(redisManager, weights);

                Console.WriteLine("Left CLI");
                tokenSource.Cancel();
                listenTask.Wait();

                //Cleanup
                Console.WriteLine("Cleanup");
                processor.Dispose();
                tokenSource.Dispose();
            }
        }
        
        static void HandleCli(PooledRedisClientManager manager, Weights weights)
        {
            bool abort = false;
            while (!abort) 
            {
                string line = Console.ReadLine();
                string[] parts = line.Split(' ');
                switch (parts[0])
                {
                    default:
                        Console.WriteLine("Dont know what that is? {0}", line);
                        break;

                    case "quit":
                    case "close":
                    case "stop":
                    case "exit":
                        abort = true;
                        break;



                    case "a":
                    case "add":
                        using (var redis = manager.GetClient())
                            redis.AddRangeToList(ImageProcessor.PENDING_KEY, parts.Skip(1).ToList());
                        break;



                    case "w":
                    case "weight":
                        if (double.TryParse(parts[2], out var weight))
                        {
                            weights.Set(parts[1].ToUpperInvariant(), weight);
                            weights.Save(weightPath);
                            Console.WriteLine("Weights set");
                        }
                        else
                        {
                            Console.WriteLine("Failed to turn " + parts[2] + " into a number");
                        }
                        break;

                }
            }
        }
    }
}
