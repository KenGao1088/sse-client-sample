using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.AspNetCore.StaticFiles;

namespace sse_client
{
    public class SSEvent
    {
        public string EventName { get; set; }
        public string Data { get; set; }
    }

    class Program
    {
        public static List<string> Queue = new List<string>(1024);
        public const string URL = "https://localhost:5001/api/notification/stream?name=user1";
        public static void Main(string[] args)
        {
            Console.Write("Opening stream...");
            var stream = Program.OpenSSEStream(URL);
            if (stream != null)
            {
                ReadStreamForever(stream);
            }
            else
            {
                Console.Write("failed to initialize, please make sure server is up first\n");
                return;
            }
            while (true)
            {
                var line = Console.ReadLine();
                if (String.IsNullOrWhiteSpace(line)) continue;
                var cmds = line.Split(" ");
                switch (cmds[0])
                {
                    case "q":
                        return;
                    case "upload":
                        if (cmds.Length < 2)
                        {
                            Console.WriteLine("please provide a filename");
                            continue;
                        }
                        Upload(cmds[1]);
                        break;

                }
            }
        }

        public static string getFileType(string fileName)
        {
            var provider = new FileExtensionContentTypeProvider();
            string contentType;
            if (!provider.TryGetContentType(fileName, out contentType))
            {
                contentType = "application/octet-stream";
            }
            return contentType;
        }
        public static async void Upload(string fileName)
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    return true;
                };
            using (HttpClient client = new HttpClient(handler))
            {
                var content = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
                var file = File.OpenRead(fileName);
                var fstreamContent = new StreamContent(file);

                fstreamContent.Headers.Add("Content-Type", getFileType(fileName));
                content.Add(new StringContent("user1"), "receiver");
                content.Add(new StringContent("this is a message"), "message");
                content.Add(fstreamContent, "myFile", fileName);

                Console.WriteLine("uploading...");
                var response = await client.PostAsync("https://localhost:5001/api/notification/sendattachment", content);
                Console.WriteLine("done. Response:");
                Console.WriteLine(response.StatusCode);
                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseString);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string id = responseString.Split(" ")[1];
                    Console.WriteLine($"download link: https://localhost:5001/api/notification/download/{id}");
                }
            }
        }

        public static Stream OpenSSEStream(string url)
        {
            var request = WebRequest.Create(new Uri(url));
            ((HttpWebRequest)request).AllowReadStreamBuffering = false;
            // FIXME provide a valid ssl for production and remove this hack
            ((HttpWebRequest)request).ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
            try
            {
                var response = request.GetResponse();
                var stream = response.GetResponseStream();
                return stream;

            }
            catch (WebException)
            {
                return null;
            }
        }

        public static async Task<Stream> retry()
        {
            Random random = new Random();
            int retryCount = 0;
            int backoff = Convert.ToInt32(Math.Floor(1000 * random.NextDouble()));
            // minimum 150
            if (backoff < 150)
            {
                backoff += 150;
            }
            Stream ret = null;
            while (ret == null)
            {
                retryCount++;
                Console.WriteLine($"retry #{retryCount} waiting for {backoff}ms");
                await Task.Delay(backoff + 1000); // 1s + x ms
                backoff *= 2;
                //maximum 30s wait time
                if (backoff > 30000)
                {
                    backoff = 30000;
                }
                ret = OpenSSEStream(URL);
            }
            return ret;
        }

        // make this async
        public static async void ReadStreamForever(Stream stream)
        {
            var encoder = new UTF8Encoding();
            var buffer = new byte[2048];
            Console.Write("Stream opened, waiting for event.. \n");
            while (true)
            {
                // can use async event with callback here
                if (stream.CanRead)
                {
                    try
                    {
                        int len = stream.Read(buffer, 0, 2048);
                        if (len > 0)
                        {
                            var text = encoder.GetString(buffer, 0, len);
                            Program.Push(text);
                        }
                    }
                    catch (IOException)
                    {
                        // disconnected
                        stream = await retry();
                    }
                }

                await Task.Delay(250);
            }
        }

        public static void Push(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var lines = text.Trim().Split('\n');
            Program.Queue.AddRange(lines);

            if (text.Contains("data:"))
            {
                Program.ProcessLines();
            }
        }

        public static void ProcessLines()
        {
            var lines = Program.Queue;

            SSEvent lastEvent = null;
            int index = 0;
            int lastEventIdx = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                line = line.Trim();

                if (line.StartsWith("event:"))
                {
                    lastEvent = new SSEvent()
                    {
                        EventName = line.Replace("event:", String.Empty)
                    };
                }
                else if (line.StartsWith("data:"))
                {
                    if (lastEvent == null)
                    {
                        continue;
                    }


                    lastEvent.Data = line.Replace("data:", String.Empty);

                    Console.WriteLine("Found event: " + index);
                    Console.WriteLine("Event was: " + lastEvent.EventName);
                    Console.WriteLine("Data was: " + lastEvent.Data);
                    index++;
                    lastEventIdx = i;
                }
            }
            //trim previously processed events
            if (lastEventIdx >= 0)
            {
                lines.RemoveRange(0, lastEventIdx);
            }
        }
    }
}


