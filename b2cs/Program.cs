using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace b2cs
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("[todo: print usage text]");
        }

        static string GetBucketId(List<string> args, B2api api)
        {
            string bucketId = null;
            switch (args.ElementAtOrDefault(0))
            {
                case "-b":
                case "--bucketname":
                    string bucketName = args.ElementAtOrDefault(1);
                    foreach (B2api.ListBucketResponse.BucketResponse bucket in api.ListBuckets().Buckets)
                    {
                        if (bucket.BucketName == bucketName)
                        {
                            bucketId = bucket.BucketId;
                            break;
                        }
                    }
                    if (bucketId == null)
                    {
                        Console.WriteLine("Error: no such bucket by that name.");
                        Environment.Exit(-1);
                    }
                    break;

                case "-i":
                case "--bucketid":
                    bucketId = args.ElementAtOrDefault(1);
                    break;

                default:
                    Console.WriteLine("either \"-b|--bucketname\" or \"-i|--bucketid\" needed");
                    Environment.Exit(-1);
                    break;
            }
            return bucketId;
        }

        static void Main(string[] raw_arguments)
        {
            string accountId, apiKey;
            try
            {
                using (var authFile = File.OpenText("b2_auth.txt"))
                {
                    accountId = authFile.ReadLine();
                    apiKey = authFile.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem reading your b2_auth.txt: {0}", ex.Message);
                Console.WriteLine("This file needs to be in the current directory, and contain your account ID, followed by a newline, followed by your API key.");
                throw;
            }

            var api = new B2api(accountId, apiKey);

            if (raw_arguments.Length == 0)
            {
                PrintUsage();
                return;
            }

            List<string> args = raw_arguments.ToList();

            if (args[0] == "-r" || args[0] == "--raw")
            {
                Table.PrintRaw = true;
                args = args.Skip(1).ToList();
            }

            switch (args.ElementAtOrDefault(0))
            {
                case "help":
                case "--help":
                    PrintUsage();
                    break;

                case "buckets":
                    {
                        var table = new Table("name", "bucketId", "type");
                        foreach (B2api.ListBucketResponse.BucketResponse bucket in api.ListBuckets().Buckets)
                        {
                            table.Add(bucket.BucketName, bucket.BucketId, bucket.BucketType);
                        }
                        table.Print();
                    }
                    break;

                case "ls":
                    {
                        args = args.Skip(1).ToList();
                        string bucketId = GetBucketId(args, api);
                        var table = new Table("filename", "size", "upload timestamp", "fileId", "action");
                        foreach (B2api.ListFilesResponse.FileResponse file in api.ListFiles(bucketId: bucketId).Files)
                        {
                            DateTime timestamp = (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds(double.Parse(file.UploadTimestamp));
                            table.Add(
                                file.FileName,
                                file.Size,
                                timestamp.ToShortDateString() + " " + timestamp.ToShortTimeString(),
                                file.FileId,
                                file.Action);
                        }
                        table.Print();
                    }
                    break;

                case "upload":
                    {
                        args = args.Skip(1).ToList();
                        string bucketId = GetBucketId(args, api);
                        string filename = args.ElementAtOrDefault(2);
                        string sha1 = null;
                        using (var sha1sums = File.OpenText("sha1sums"))
                        {
                            string line;
                            while ((line = sha1sums.ReadLine()) != null)
                            {
                                if (line.EndsWith("*" + filename))
                                {
                                    sha1 = line.Substring(0, line.IndexOf(' '));
                                    break;
                                }
                            }
                        }
                        if (sha1 == null)
                        {
                            Console.WriteLine("You need to add an entry in 'sha1sums' for this file.");
                            Environment.Exit(-1);
                        }

                        long lastUpdatePosition = 0;
                        Action<long, long, TimeSpan> progressCallback = (long position, long total, TimeSpan elapsed) =>
                        {
                            Console.WriteLine("{0:N2}% ({1} / {2}) @ {3:N0}kiB/s", (double)position / (double)total * 100, position, total, (double)(position - lastUpdatePosition) / 1024 / elapsed.TotalSeconds);
                            lastUpdatePosition = position;
                        };

                        B2api.UploadResponse? response = api.Upload(bucketId, sha1, filename, progressCallback);
                        if (response.HasValue)
                        {
                            Console.WriteLine("Done.");
                            foreach (var prop in response.GetType().GetProperties())
                            {
                                Console.WriteLine("{0}: {1}", prop.Name, prop.GetValue(response, null));
                            }
                        }
                    }
                    break;
            }
        }
    }
}
