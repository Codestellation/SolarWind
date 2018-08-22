using System;

namespace Codestellation.SolarWind
{
    public class ServerOptions
    {
        public ServerOptions(Uri uri, BeforeChannelAccepted before, AfterChannelAccepted after)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
        }

        public Uri Uri { get; }
        public BeforeChannelAccepted Before { get; }
        public AfterChannelAccepted After { get; }
    }
}