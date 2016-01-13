using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using System.Threading;

namespace WebApiAsync.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        /// <summary>
        /// http://www.asp.net/mvc/overview/performance/using-asynchronous-methods-in-aspnet-mvc-4
        /// </summary>
        /// <returns></returns>
        // GET: api/values
        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            await Task.Delay(1000);

            return new string[] { "value1", "value2" };
        }
    }
}
