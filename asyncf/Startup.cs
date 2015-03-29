using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(asyncf.Startup))]
namespace asyncf
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
