namespace Codestellation.SolarWind
{
    /// <summary>
    /// Delegate which is called after channel was opened on a server <see cref="SolarWindHub"/>
    /// </summary>
    /// <param name="channelId">An id of the opened channel</param>
    /// <param name="channel">The opened channel</param>
    public delegate void AfterChannelAccepted(ChannelId channelId, Channel channel);
}