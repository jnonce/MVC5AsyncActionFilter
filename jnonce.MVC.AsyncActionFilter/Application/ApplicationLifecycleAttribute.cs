using System;
using System.Reflection;
using System.Threading.Tasks;

namespace jnonce.MVC.AsyncActionFilter.Application
{
    /// <summary>
    /// Indicates a method to run wrapping the lifecycle of the application
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ApplicationLifecycleAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationLifecycleAttribute"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="methodName">Name of the method.</param>
        public ApplicationLifecycleAttribute(Type type, string methodName)
        {
            this.Type = type;
            this.MethodName = methodName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationLifecycleAttribute"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="order">The order.</param>
        public ApplicationLifecycleAttribute(Type type, string methodName, int order)
        {
            this.Type = type;
            this.MethodName = methodName;
            this.Order = order;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        public Type Type
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the name of the method.
        /// </summary>
        public string MethodName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the value which indicates the ascending order in which to execute the startup methods.
        /// </summary>
        public int Order
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates the delegate.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">
        /// </exception>
        public Func<Func<Task>, Task> CreateDelegate()
        {
            // Get the method
            MethodInfo method = Type.GetMethod(
                MethodName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            if (method == null)
            {
                throw new ArgumentException(
                    String.Format("The type {0} doesn't have a static method named {1}",
                        Type, MethodName));
            }

            ParameterInfo[] parameters = method.GetParameters();

            if (method.ReturnType != typeof(Task)
                || parameters.Length != 1
                || parameters[0].ParameterType != typeof(Func<Task>))
            {
                throw new ArgumentException(
                    String.Format("The method {0}.{1} has a bad signature.",
                        Type, MethodName));
            }

            return (Func<Func<Task>, Task>)method
                .CreateDelegate(typeof(Func<Func<Task>, Task>));
        }
    }
}
