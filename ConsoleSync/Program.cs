using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
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
                TestResult testResult = new TestResult();

                Stopwatch sw = new Stopwatch();
                sw.Start();

                switch (selectedChar)
                {
                    case 's':
                        SynchronousTest(testResult);
                        break;
                    case 'a':
                        AsynchronousTest(testResult).Wait();
                        break;
                    case 'p':
                        ParallelTest(testResult);
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
                Trace.WriteLine(string.Format("Total Html Processed : {0}", testResult.TotalHtmlProcessed));
                Trace.WriteLine(string.Format("Elapsed Time : {0}", sw.Elapsed));
                Trace.WriteLine(string.Format("Memory : {0}MB", currentProcess.PeakWorkingSet64 / 1024));
                Trace.WriteLine(new string('*', 30));

                Trace.Flush();
            }
        }

        public static void SynchronousTest(TestResult testResult)
        {
            for (int i = 0; i < TOTAL_REQUEST; i++)
            {
                string html = GetHtml(i);
                ProcessHtml(testResult, html);
            }
        }

        private static void ProcessHtml(TestResult testResult, string html)
        {
            testResult.TotalHtmlProcessed++;
        }

        /// <summary>
        /// Creating a new tread is costly, it takes time. Unless we need to control a thread, then “Task-based Asynchronous Pattern (TAP)” and “Task Parallel Library (TPL)” is good enough for asynchronous and parallel programming. TAP and TPL uses Task (we will discuss what is Task latter). In general Task uses the thread from ThreadPool(A thread pool is a collection of threads already created and maintained by .NET framework. If we use Task, most of the cases we need not to use thread pool directly. Still if you want to know more about thread pool visit the link: https://msdn.microsoft.com/en-us/library/h4732ks0.aspx)
        /// </summary>
        /// <param name="testResult"></param>
        /// <returns></returns>
        public async static Task AsynchronousTest(TestResult testResult)
        {
            List<Task> taskList = new List<Task>();

            for (int i = 0; i < TOTAL_REQUEST; i++)
            {
                Task taskGetHtmlAsync =
                    GetHtmlAsync(i)
                    .ContinueWith((taskResult) =>
                    {
                        ProcessHtml(testResult, taskResult.Result);
                    });
                taskList.Add(taskGetHtmlAsync);
            }

            await Task.WhenAll(taskList);
        }

        public static void ParallelTest(TestResult testResult)
        {
            Parallel.For(0, TOTAL_REQUEST, i =>
            {
                string html = GetHtml(i);
                ProcessHtml(testResult, html);
            });
        }

        public async static Task<string> GetHtmlAsync(int i)
        {
            string url = GetUrl(i);

            using (HttpClient client = new HttpClient(
                new WebRequestHandler {
                    CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.BypassCache)
                }))
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
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);

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
