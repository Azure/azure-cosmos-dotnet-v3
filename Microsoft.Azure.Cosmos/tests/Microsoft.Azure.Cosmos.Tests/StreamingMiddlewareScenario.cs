namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;

    /// Generic MVC controller are strongly typed => pay the ser-de cost 
    ///         Request  Path: stream (Incoming reqeust) -> Type -> stream (Outgoing request)
    ///         Response Path: stram (Incoming response) <- type - stram (Outgoing response)
    ///         
    /// >Performance path should avoud unnecessary 
    public class StreamingMiddlewareScenario
    {
        public static async Task Test()
        {
            await Task.Yield();
            throw new NotImplementedException();
        }
    }

    ////public class StreamPipelineController : Controller
    ////{
    ////    private readonly ToDoDbContext _dbContext;

    ////    public ToDoController(ToDoDbContext context) =>
    ////        _dbContext = context;

    ////    [HttpGet]
    ////    public IEnumerable GetAll() =>
    ////        _dbContext.ToDoItems.ToList();
    ////}
}