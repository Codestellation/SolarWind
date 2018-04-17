using System;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Represents options which are used to setup listener at solarwind hub
    /// </summary>
    public class ServerOptions
    {
        /// <summary>
        /// Initialized a new instance of <see cref="ServerOptions" /> class
        /// </summary>
        /// <param name="uri">An uri to listen at</param>
        /// <param name="before">Action that is invoked before a channel accepted.</param>
        /// <param name="after">An action that is invoked after channel is created.</param>
        public ServerOptions(Uri uri, BeforeChannelAccepted before, AfterChannelAccepted after)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
        }

        /// <summary>
        /// An uri to listen at
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Action that is invoked before a channel accepted.
        /// </summary>
        public BeforeChannelAccepted Before { get; }

        /// <summary>
        /// An action that is invoked after channel is created.
        /// </summary>
        public AfterChannelAccepted After { get; }
    }
}