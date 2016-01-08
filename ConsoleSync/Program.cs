using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleSync
{
    /// <summary>
    /// Referências:
    /// http://stackoverflow.com/questions/17630506/async-at-console-app-in-c
    /// 
    /// http://stackoverflow.com/questions/14099520/async-await-and-parallel-in-c-sharp
    /// http://www.codeproject.com/Articles/996857/Asynchronous-programming-and-Threading-in-Csharp-N
    /// https://msdn.microsoft.com/pt-br/library/hh191443.aspx 
    ///
    /// </summary>
    class Program
    {
        const int TOTAL_REQUEST = 250;
        const int TOTAL_TEST = 1;

        static void Main(string[] args)
        {
            Console.WriteLine("Choose the test type \n s = synchronous \n a = asynchronous \n p = parallel");

            char selectedChar = ' ';

            while (selectedChar != 's' && selectedChar != 'a' && selectedChar != 'p')
            {
                selectedChar = Convert.ToChar(Console.Read());
            }

            //Writing trace in console
            Trace.Listeners.Add(new ConsoleTraceListener());

            //Repeating the test if needed
            for (int i = 0; i < TOTAL_TEST; i++)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                switch (selectedChar)
                {
                    case 's':
                        SynchronousTest();
                        break;
                    case 'a':
                        AsynchronousTest().Wait();
                        break;
                    case 'p':
                        ParallelTest();
                        break;
                    default:
                        break;
                }

                sw.Stop();

                Trace.Listeners.Add(new TextWriterTraceListener(string.Format("{0}(reqs={1})-{2:yyyyMMdd HHmmss}.log", selectedChar, TOTAL_REQUEST, DateTime.Now),
                    "myListener"));

                // get the current process
                Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                Trace.WriteLine(new string('*',30));
                Trace.WriteLine(string.Format("Test Number : {0}", i + 1));
                Trace.WriteLine(string.Format("Elapsed Time : {0}", sw.Elapsed));
                Trace.WriteLine(string.Format("Memory : {0}MB", currentProcess.PeakWorkingSet64 / 1024));
                Trace.WriteLine(new string('*', 30));

                Trace.Flush();
            }
        }

        public static void SynchronousTest()
        {
            for (int i = 0; i < TOTAL_REQUEST; i++)
            {
                string html = GetHtml(i);
            }
        }

        public async static Task AsynchronousTest()
        {
            List<Task<string>> taskList = new List<Task<string>>();

            for (int i = 0; i < TOTAL_REQUEST; i++)
            {
                Task<string> taskGetHtmlAsync = GetHtmlAsync(i);
                taskList.Add(taskGetHtmlAsync);
            }

            await Task.WhenAll(taskList);

            //Trying to free memory
            taskList.ForEach(t => t.Dispose());
            taskList.Clear();
            taskList = null;
            GC.Collect();
        }

        public static void ParallelTest()
        {
            Parallel.For(0, TOTAL_REQUEST, i =>
            {
                string html = GetHtml(i);
            });
        }

        public async static Task<string> GetHtmlAsync(int i)
        {
            string url = GetUrl(i);

            using (HttpClient client = new HttpClient())
            {
                string html;

                html = await client.GetStringAsync(url);

                Trace.WriteLine(string.Format("{0} - OK (Html Length {1})", i + 1, html.Length));
                return html;
            }
        }

        public static string GetHtml(int i)
        {
            string html = null;

            string url = GetUrl(i);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream receiveStream = response.GetResponseStream())
                {
                    using (StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8))
                    {
                        html = readStream.ReadToEnd();
                    }
                }
            }

            Trace.WriteLine(string.Format("{0} - OK (Html Length {1})", i + 1, html.Length));

            return html;
        }

        /// <summary>
        /// Sharing the load test in two diffent servers (To avoid overload)
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private static string GetUrl(int i)
        {
            string url;
            if (i % 2 == 0)
            {
                url = "http://msdn.microsoft.com";
            }
            else
            {
                url = "http://www.apple.com/";
            }
            //url = "http://www.google.com");

            return url;
        }

        public async static Task<DateTime> GetStartDateTimeAsync(int i)
        {
            DateTime startedDateTime = DateTime.Now;
            await Task.Delay(2000);
            Trace.WriteLine(string.Format("{0} - OK (Started DateTime {1})", i + 1, startedDateTime));
            return startedDateTime;
        }
    }
}
