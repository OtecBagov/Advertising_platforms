namespace Advertising_platforms.Exceptions;

public class AdvertisingInvalidDataException : AdvertisingPlatformException
{
    public AdvertisingInvalidDataException(string message) : base(message)
    {
    }

    public AdvertisingInvalidDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
} 