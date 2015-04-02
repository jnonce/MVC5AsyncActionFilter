using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;

namespace jnonce.MVC.AsyncActionFilter.Application
{
    /// <summary>
    /// Tracks startup and shutdown for Asp.net
    /// </summary>
    public class LifecycleManager
    {
        private static ManualResetEventSlim dataInitializationField;
        private static bool dataIsInitialized;
        private static object dataLock = new object();

        private static ManualResetEventSlim readyToStart;
        private static ManualResetEventSlim readyToStop;
        private static TaskCompletionSource<object> shuttingDown;

        /// <summary>
        /// Runs this instance.
        /// </summary>
        public static void Run()
        {
            LazyInitializer.EnsureInitialized(
                ref dataInitializationField,
                ref dataIsInitialized,
                ref dataLock,
                Initialize);

            // Block until startup methods are ready for the web server to execute
            readyToStart.Wait();
        }

        private static ManualResetEventSlim Initialize()
        {
            // Initialize synchronization objects
            readyToStart = new ManualResetEventSlim();
            readyToStop = new ManualResetEventSlim();
            shuttingDown = new TaskCompletionSource<object>();

            // Get startup methods
            var methods = GetLifecycleMethods();

            // Start all methods
            RunAll(methods);

            // Register a module so that we'll be able to track shutdown
            Microsoft.Web.Infrastructure.DynamicModuleHelper.DynamicModuleUtility.RegisterModule(typeof(ShutdownTrackingModule));

            return readyToStart;
        }

        private static Task RunAll(Func<Func<Task>, Task>[] activation)
        {
            // Create the initial method which, when called, will "run" the webserver.
            Func<Task> executionFunc = new Func<Task>(() =>
                {
                    readyToStart.Set();
                    return shuttingDown.Task;
                });

            // Add each additional activation method as a layer of wrap
            for (int i = activation.Length - 1; i >= 0; i--)
            {
                executionFunc = CreateExecutionFunc(activation[i], executionFunc);
            }

            // Once the method chain is arranged we start it up
            // The resultant outer task is then tracked such that the readyToStop event gets triggered
            // when all the tasks are finished.
            return executionFunc().ContinueWith(_ => readyToStop.Set());
        }

        private static Func<Task> CreateExecutionFunc(Func<Func<Task>, Task> outerMethod, Func<Task> innerMethod)
        {
            // Wrap the inner method in Lazy so that the method can safely call it multiple times.
            Lazy<Task> idempotentInnerMethod = new Lazy<Task>(innerMethod, LazyThreadSafetyMode.ExecutionAndPublication);

            // Create a method that, when called will schedule a task.
            // The task invokes the startup method.
            // Chain a call at the end to ensure that the next method gets called (even if the 
            return () => Task.Run(
                () => outerMethod(() => idempotentInnerMethod.Value))
                .ContinueWith(_ => idempotentInnerMethod.Value)
                ;
        }

        private static IReadOnlyCollection<Assembly> GetAssemblies()
        {
            var assemblies = new List<Assembly>();
            foreach (var assemblyFile in GetAssemblyFiles())
            {
                try
                {
                    assemblies.Add(Assembly.LoadFrom(assemblyFile));
                }
                catch
                {
                    // Ignore assembly load errors for now
                }
            }

            return assemblies;
        }

        private static IEnumerable<string> GetAssemblyFiles()
        {
            string directory = HostingEnvironment.IsHosted
                ? HttpRuntime.BinDirectory
                : Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            return Directory.GetFiles(directory, "*.dll");
        }

        // Call the relevant activation method from all assemblies
        private static Func<Func<Task>, Task>[] GetLifecycleMethods()
        {
            return GetAssemblies()
                .SelectMany(assembly => assembly.GetCustomAttributes<ApplicationLifecycleAttribute>())
                .OrderBy(a => a.Order)
                .Select(att => att.CreateDelegate())
                .ToArray();
        }

        private class ShutdownTrackingModule : IHttpModule
        {
            private static int count = 0;

            public void Init(HttpApplication context)
            {
                Interlocked.Increment(ref count);
            }

            public void Dispose()
            {
                if (Interlocked.Decrement(ref count) == 0)
                {
                    if (shuttingDown.TrySetResult(new object()))
                    {
                        readyToStop.Wait();
                    }
                }
            }
        }
    }
}
