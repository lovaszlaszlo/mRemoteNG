namespace mRemoteNG.Connection.Protocol
{
    /// <summary>
    /// A protocol that can carry another connection's SSH tunnel.
    /// </summary>
    /// <remarks>
    /// <see cref="ConnectionInitiator"/> opens the connection named as a connection's SSH tunnel,
    /// having appended a -L forward to its options, and then waits for the local port to answer.
    /// It used to demand a <see cref="PuttyBase"/> outright, which was fair while PuTTY was the
    /// only protocol here that could forward a port at all. The native SSH protocol forwards ports
    /// itself now, so the requirement is expressed as this instead: not "be PuTTY", but "be able to
    /// do the job and say whether you are still up".
    /// </remarks>
    public interface ISshTunnelProvider
    {
        /// <summary>
        /// False once this session is known to have failed or ended, true while it may yet carry
        /// the tunnel.
        /// </summary>
        /// <remarks>
        /// Deliberately not "is connected". The waiting loop treats false as a lost cause and
        /// gives up, so a session that is still in the middle of connecting has to answer true -
        /// otherwise it would be abandoned in the moment before it succeeded.
        /// </remarks>
        bool IsTunnelRunning { get; }
    }
}
