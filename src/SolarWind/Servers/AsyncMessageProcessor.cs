using System.Threading.Tasks;

namespace Codestellation.SolarWind.Servers
{
    public delegate ValueTask<object> AsyncMessageProcessor(object data);
}