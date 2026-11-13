namespace Application.Protocol
{
    public enum TankFrameError
    {
        Empty,
        MissingSoh,
        MissingEtx,
        MissingTerminator,
        BadChecksumLength,
        ChecksumMismatch,
        UnknownCommand,
        Truncated,
        InvalidDate,
        InvalidFloat,
        InvalidField,
    }

    /// <summary>Error tipado al desenvolver o leer una trama.</summary>
    public sealed class TankFrameException : Exception
    {
        public TankFrameError Error { get; }

        public TankFrameException(TankFrameError error, string message) : base(message)
        {
            Error = error;
        }
    }
}
