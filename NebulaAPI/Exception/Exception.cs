namespace Virial;

public class NonOwnerPlayerException : Exception
{
    public NonOwnerPlayerException(string? message, Exception? innerException = null) : base(message ?? "Non-owner client cannot do such operation.", innerException){}
}

public class NonHostPlayerException : Exception
{
    public NonHostPlayerException(string? message, Exception? innerException = null) : base(message ?? "Non-host client cannot do such operation.", innerException) { }
}
