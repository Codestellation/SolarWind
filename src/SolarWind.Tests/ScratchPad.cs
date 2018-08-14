using System.Net.Sockets;
using System.Threading.Tasks;
using Codestellation.SolarWind.Misc;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    public class ScratchPad
    {
        [Test]
        public void Test()
        {
            var server = new TcpListener(4455);
            server.Start();
            server.AcceptTcpClientAsync().ContinueWith(OnAccepted);

            var client = Build.TcpIPv4();
            client.Connect("localhost", 4455);

            //var sendArgs = 
        }

        private void OnAccepted(Task<TcpClient> obj)
        {


        }
    }
}