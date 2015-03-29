# MVC5AsyncActionFilter

MVC6 has an [`ActionFilterAttribute`][ActionFilterAttribute] which supports `IAsyncActionFilter`.  WebAPI supports [`ActionFilterAttribute.OnActionExecutedAsync`][WebAPIAsync] for some async support.  There isn't an equivalent in MVC5.

That's unfortunate.  Potentially that implies blocking a thread while asyc operations
are underway, but it also prevents us from using async/await during filtering.  Well, I without doing something much more drastic we can at least give the impression of async filters and allow filters to be written in the form of an async operation.

To allow this we introduce `AsyncActionFilterAttribute` as an abstract base class.  You can take advantage of this in something of the form:

```cs
public class MyAsyncFilter : AsyncActionFilterAttribute
{

    protected override async Task OnRequest(
        AsyncActionFilterAttribute.IRequestContext filterContext)
    {
        // This code is executed before the action is invoked
        // Use filterContext.ActionExecuting (ActionExecutingContext) for context

        // Request the execution of the action and await the results
        ActionExecutedContext actionExecutedContext = await filterContext.ExecuteAction();

        // Code here runs before any result/view processing has started

        // Request that we finish all action filters
        ResultExecutingContext resultExecutingContext = await filterContext.CompleteActionProcessing();

        // Code here runs before the results/view are executed

        // Request that the results are executed (view is invoked)
        ResultExecutedContext resultExecutedContext = await filterContext.ExecuteResult();

        // Code here is executed after the view
    }
}
```

[ActionFilterAttribute]:https://github.com/aspnet/Mvc/blob/dev/src/Microsoft.AspNet.Mvc.Core/Filters/ActionFilterAttribute.cs "opt"
[WebAPIAsync]:https://msdn.microsoft.com/en-us/library/system.web.http.filters.actionfilterattribute.onactionexecutedasync%28v=vs.118%29.aspx
