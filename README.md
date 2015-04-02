# MVC5AsyncActionFilter

MVC6 has an [`ActionFilterAttribute`][ActionFilterAttribute] which supports `IAsyncActionFilter`.  WebAPI supports [`ActionFilterAttribute.OnActionExecutedAsync`][WebAPIAsync] for some async support.  There isn't an equivalent in MVC5.

That's unfortunate.  Potentially that implies blocking a thread while asyc operations
are underway, but it also prevents us from using async/await during filtering.  Well, I without doing something much more drastic we can at least give the impression of async filters and allow filters to be written in the form of an async operation.

To allow this we introduce `AsyncActionFilterAttribute` as an abstract base class.  You can take advantage of this in something of the form:

```cs
public class MyAsyncFilter : AsyncActionFilterAttribute
{

    protected override async Task OnRequest(IActionSequencer sequencer)
    {
        // This code is executed before the action is invoked
        // Use sequencer.ActionExecuting (ActionExecutingContext) for context

        // Request the execution of the action and await the results
        ActionExecutedContext actionExecutedContext = await sequencer.ExecuteAction();

        // Code here runs before any result/view processing has started

        // Request that we finish all action filters
        ResultExecutingContext resultExecutingContext = await sequencer.CompleteActionProcessing();

        // Code here runs before the results/view are executed

        // Request that the results are executed (view is invoked)
        ResultExecutedContext resultExecutedContext = await sequencer.ExecuteResult();

        // Code here is executed after the view
    }
}
```

[ActionFilterAttribute]:https://github.com/aspnet/Mvc/blob/dev/src/Microsoft.AspNet.Mvc.Core/Filters/ActionFilterAttribute.cs "opt"
[WebAPIAsync]:https://msdn.microsoft.com/en-us/library/system.web.http.filters.actionfilterattribute.onactionexecutedasync%28v=vs.118%29.aspx

## ApplicationLifecycle

Asp.net supports `PreApplicationStartMethodAttribute`, used to indicate a method which Asp.net
will run before processing requests.  [WebActivator] extended this to support
multiple routines in a single DLL, psot startup methods, as well as shutdown methods.

In this library we do something similar, but rather than support separate startup and shutdown
we support a single, async method.  This is used as such:

```cs
[assembly: ApplicationLifecycle(typeof(MyClass), "RunMe")]

public static class MyClass
{
    public static async Task RunMe(Func<Task> next)
    {
        // This runs during before the webserver takes requests.

        // Call next() to trigger startup for the webserver.
        // That call will return a Task which will complete when the server is finished
        // taking requests and begins shutdown.
        await next();

        // This code runs when the server is shutting down.
    }
}
```

This makes symmetric startup and shutdown behaviors fit into a single logical flow.

[WebActivator]:https://github.com/davidebbo/WebActivator
