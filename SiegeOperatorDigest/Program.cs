using System;
using System.Threading;

namespace SiegeOperatorDigest
{
    class Program
    {
        static void Main(string[] args)
        {
            string username = null;
            string accessKey = "Hfkd67ASfbasf";
            bool cleanDirectory = true;
            bool cleanImages = true;
            int scanRate = 5000;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    default:
                        Console.WriteLine("Unkown Command {0}", args[i]);
                        break;

                    case "-username":
                        username = args[++i];
                        break;

                    case "-access_key":
                        accessKey = args[++i];
                        break;

                    case "-dont_purge":
                        cleanDirectory = false;
                        break;

                    case "-dont_purge_images":
                        cleanImages = false;
                        break;

                    case "-scan":
                        scanRate = int.Parse(args[++i]);
                        break;


                }

            }

            while (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("Failed to give -username. What is your Ubisoft Username?");
                username = Console.ReadLine();
            }

            var digest = new Digest(username, accessKey)
            {
                DeleteImages = cleanImages,
                ScanRate = scanRate
            };


            if (cleanDirectory)
                digest.CleanCaptureDirectory().Wait();

            using (var tokenSource = new CancellationTokenSource())
            {
                var task = digest.ContinouslyScan(tokenSource.Token);
                Console.WriteLine("Scanning Continously. Press anykey to exit.");
                Console.ReadKey();
                tokenSource.Cancel();
                try { task.Wait(); } catch { }
            }
        }
    }
}
