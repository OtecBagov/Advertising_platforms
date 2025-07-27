namespace Advertising_platforms.Exceptions;

public class AdvertisingFileLoadException : AdvertisingPlatformException
{
    public AdvertisingFileLoadException(string message) : base(message)
    {
    }

    public AdvertisingFileLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }
} 