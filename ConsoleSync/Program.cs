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
    /// http://stackoverflow.com/questions/16194054/is-async-httpclient-from-net-4-5-a-bad-choice-for-intensive-load-applications
    /// 
    /// The best way to use httpclient is sharing a single instance across multiple threads:
    /// http://stackoverflow.com/questions/27732546/httpclienthandler-httpclient-memory-leak
    /// </summary>
    class Program
    {
        const int TOTAL_REQUEST = 500;
        const int TOTAL_TEST = 1;
        static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromMinutes(1);

        static void Main(string[] args)
        {
            Console.WriteLine("Choose the test type");
            Console.WriteLine("0 = synchronous");
            Console.WriteLine("1 = asynchronous (WebRequest)");
            Console.WriteLine("2 = asynchronous (Httpclient)");
            Console.WriteLine("3 = asynchronous (Httpclient Memory Leak)");
            Console.WriteLine("4 = parallel (WebRequest)");
            Console.WriteLine("5 = parallel (HttpClient)");

            int selectedTest = -1;
            string selectedTestText = "";
            bool validSelection = false;

            while (!validSelection)
            {
                char selectedTestChar = Convert.ToChar(Console.Read());

                if (!(new char[] { '0', '1', '2', '3', '4', '5' }).Contains(selectedTestChar))
                {
                    validSelection = false;
                    Console.WriteLine("Invalid selection, try again");
                }
                else
                {
                    selectedTest = Convert.ToInt32(selectedTestChar.ToString());
                    validSelection = true;
                }
            }

            //Writing trace in console
            Trace.Listeners.Add(new ConsoleTraceListener());

            //Repeating the test if needed
            for (int i = 0; i < TOTAL_TEST; i++)
            {
                TestResult testResult = new TestResult();

                Stopwatch sw = new Stopwatch();
                sw.Start();

                switch (selectedTest)
                {
                    case 0:
                        SynchronousTest(testResult);
                        selectedTestText = "SynchronousTest";
                        break;
                    case 1:
                        AsynchronousGetHtmlTest(testResult).Wait();
                        selectedTestText = "AsynchronousGetHtmlTest";
                        break;
                    case 2:
                        AsynchronousGetHtmlAsyncTest(testResult).Wait();
                        selectedTestText = "AsynchronousGetHtmlAsyncTest";
                        break;
                    case 3:
                        AsynchronousGetHtmlAsyncMemoryLeakTest(testResult).Wait();
                        selectedTestText = "AsynchronousGetHtmlAsyncMemoryLeakTest";
                        break;
                    case 4:
                        ParallelGetHtmlTest(testResult);
                        selectedTestText = "ParallelGetHtmlTest";
                        break;
                    case 5:
                        ParallelGetHtmlAsyncTest(testResult);
                        selectedTestText = "ParallelGetHtmlAsyncTest";
                        break;
                    default:
                        break;
                }

                sw.Stop();

                Trace.Listeners.Add(new TextWriterTraceListener(string.Format("{0}(reqs={1})-{2:yyyyMMdd HHmmss}.log", selectedTestText, TOTAL_REQUEST, DateTime.Now),
                    "myListener"));

                // get the current process
                Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                long processPeakWorkingSetMB = currentProcess.PeakWorkingSet64 / 1024;

                Trace.WriteLine(new string('*',30));
                Trace.WriteLine(string.Format("Test Type : {0}", selectedTestText));
                Trace.WriteLine(string.Format("Test Number : {0}", i + 1));
                Trace.WriteLine(string.Format("Total Html Processed : {0}", testResult.TotalHtmlProcessed));
                Trace.WriteLine(string.Format("Elapsed Time : {0}", sw.Elapsed));
                Trace.WriteLine(string.Format("Memory : {0}MB", processPeakWorkingSetMB));
                Trace.WriteLine(string.Format("Thread Count : {0}", currentProcess.Threads.Count));
                Trace.WriteLine(new string('*', 30));
                Trace.WriteLine(new string('*', 30));
                Trace.WriteLine("Copy to Excel:");
                Trace.WriteLine(sw.Elapsed.TotalSeconds);
                Trace.WriteLine(processPeakWorkingSetMB);
                Trace.WriteLine(currentProcess.Threads.Count);
                Trace.WriteLine(new string('*', 30));

                Trace.Flush();

                //To being able to see the results before closing the console
                Thread.Sleep(5000);
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
        /// Elapsed Time : 00:00:46.9598128
        /// Memory : 36552MB
        /// 
        /// Creating a new tread is costly, it takes time. Unless we need to control a thread, then “Task-based Asynchronous Pattern (TAP)” and “Task Parallel Library (TPL)” is good enough for asynchronous and parallel programming. TAP and TPL uses Task (we will discuss what is Task latter). In general Task uses the thread from ThreadPool(A thread pool is a collection of threads already created and maintained by .NET framework. If we use Task, most of the cases we need not to use thread pool directly. Still if you want to know more about thread pool visit the link: https://msdn.microsoft.com/en-us/library/h4732ks0.aspx)
        /// </summary>
        /// <param name="testResult"></param>
        /// <returns></returns>
        public async static Task AsynchronousGetHtmlAsyncTest(TestResult testResult)
        {
            List<Task> taskList = new List<Task>();

            using (HttpClient client = new HttpClient(
                new WebRequestHandler
                {
                    CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.BypassCache)
                }))
            {
                client.Timeout = REQUEST_TIMEOUT;

                for (int i = 0; i < TOTAL_REQUEST; i++)
                {
                    int callIndex = i;

                    Task taskGetHtmlAsync =
                        GetHtmlAsync(client, callIndex)
                        .ContinueWith((taskResult) =>
                        {
                            ProcessHtml(testResult, taskResult.Result);
                        });
                    taskList.Add(taskGetHtmlAsync);
                }

                await Task.WhenAll(taskList);
            }

            taskList.ForEach(t => t.Dispose());
            taskList.Clear();
            taskList = null;
        }

        /// <summary>
        /// Elapsed Time : 00:00:46.9598128
        /// Memory : 36552MB
        /// 
        /// Creating a new tread is costly, it takes time. Unless we need to control a thread, then “Task-based Asynchronous Pattern (TAP)” and “Task Parallel Library (TPL)” is good enough for asynchronous and parallel programming. TAP and TPL uses Task (we will discuss what is Task latter). In general Task uses the thread from ThreadPool(A thread pool is a collection of threads already created and maintained by .NET framework. If we use Task, most of the cases we need not to use thread pool directly. Still if you want to know more about thread pool visit the link: https://msdn.microsoft.com/en-us/library/h4732ks0.aspx)
        /// </summary>
        /// <param name="testResult"></param>
        /// <returns></returns>
        public async static Task AsynchronousGetHtmlAsyncMemoryLeakTest(TestResult testResult)
        {
            List<Task> taskList = new List<Task>();

            for (int i = 0; i < TOTAL_REQUEST; i++)
            {
                int callIndex = i;
                Task taskGetHtmlAsync =
                    GetHtmlAsyncMemoryLeak(callIndex)
                    .ContinueWith((taskResult) =>
                    {
                        ProcessHtml(testResult, taskResult.Result);
                    });
                taskList.Add(taskGetHtmlAsync);
            }

            await Task.WhenAll(taskList);

            taskList.ForEach(t => t.Dispose());
            taskList.Clear();
            taskList = null;
        }

        /// <summary>
        /// Elapsed Time : 00:01:41.1977040
        /// Memory : 35304MB
        /// </summary>
        /// <param name="testResult"></param>
        /// <returns></returns>
        public async static Task AsynchronousGetHtmlTest(TestResult testResult)
        {
            List<Task> taskList = new List<Task>();

            for (int i = 0; i < TOTAL_REQUEST; i++)
            {
                int callIndex = i;
                Task taskGetHtmlAsync = Task.Run<string>(() => GetHtml(callIndex))
                    .ContinueWith((taskResult) =>
                    {
                        ProcessHtml(testResult, taskResult.Result);
                    });
                taskList.Add(taskGetHtmlAsync);
            }

            await Task.WhenAll(taskList);

            taskList.ForEach(t => t.Dispose());
            taskList.Clear();
            taskList = null;
        }

        /// <summary>
        /// Elapsed Time : 00:00:49.9561993
        /// Memory : 33840MB
        /// </summary>
        /// <param name="testResult"></param>
        public static void ParallelGetHtmlTest(TestResult testResult)
        {
            Parallel.For(0, TOTAL_REQUEST, i =>
            {
                string html = GetHtml(i);
                ProcessHtml(testResult, html);
            });
        }

        /// <summary>
        /// Elapsed Time : 00:00:45.1999532
        /// Memory : 36456MB
        /// </summary>
        /// <param name="testResult"></param>
        public static void ParallelGetHtmlAsyncTest(TestResult testResult)
        {
            using (HttpClient client = new HttpClient(
                new WebRequestHandler
                {
                    CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.BypassCache)
                }))
            {

                Parallel.For(0, TOTAL_REQUEST, i =>
                {
                    string html = GetHtmlAsync(client, i).Result;
                    ProcessHtml(testResult, html);
                });
            }
        }

        public async static Task<string> GetHtmlAsync(HttpClient client, int i)
        {
            string url = GetUrl();

            Task<string> getHtmlTask;
            string html;

            using (getHtmlTask = client.GetStringAsync(url))
            {
                html = await getHtmlTask;

                Trace.WriteLine(string.Format("{0} - OK (Html Length {1})", i + 1, html.Length));
            }

            return html;
        }

        public async static Task<string> GetHtmlAsyncMemoryLeak(int i)
        {
            string url = GetUrl();

            string html;

            using (HttpClient client = new HttpClient(
                new WebRequestHandler
                {
                    CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.BypassCache)
                }))
            {
                client.Timeout = REQUEST_TIMEOUT;

                Task<string> getHtmlTask;

                using (getHtmlTask = client.GetStringAsync(url))
                {
                    html = await getHtmlTask;

                    Trace.WriteLine(string.Format("{0} - OK (Html Length {1})", i + 1, html.Length));
                }
            }

            return html;
        }

        public static string GetHtml(int i)
        {
            string html = null;

            string url = GetUrl();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = Convert.ToInt32(REQUEST_TIMEOUT.TotalMilliseconds);
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
        private static string GetUrl()
        {
            return "http://localhost:63547/api/values";
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
