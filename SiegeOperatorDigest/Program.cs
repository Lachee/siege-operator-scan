using ImageMagick;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;



namespace SiegeOperatorDigest
{
    class Program
    {
        static void Main(string[] args)
        {
            string username = null;
            string accessKey = "Hfkd67ASfbasf";
            int uploadRate = 10;
            int bufferSize = 1280 * 720;

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

                    case "-rate":
                        uploadRate = int.Parse(args[++i]);
                        break;

                    case "-buffer":
                        bufferSize = int.Parse(args[++i]);
                        break;

                }

            }

            while (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("Failed to give -username. What is your Ubisoft Username?");
                username = Console.ReadLine();
            }

            var digest = new PipeDigest(username, accessKey, bufferSize)
            {
                UploadRate = uploadRate
            };
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
