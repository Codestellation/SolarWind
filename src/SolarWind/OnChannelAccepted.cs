namespace Codestellation.SolarWind
{
    /// <summary>
    /// An instance of <see cref="SolarWindHub" /> invokes this delegate before accepting channel
    /// </summary>
    /// <param name="remoteHubId">An identifier of a remote hub</param>
    /// <returns>Returns option to create an instance of <see cref="Channel" /> with.</returns>
    public delegate ChannelOptions BeforeChannelAccepted(HubId remoteHubId);
}