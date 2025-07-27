namespace Advertising_platforms.Exceptions;

public class AdvertisingPlatformException : Exception
{
    public AdvertisingPlatformException(string message) : base(message)
    {
    }

    public AdvertisingPlatformException(string message, Exception innerException) : base(message, innerException)
    {
    }
} 