namespace Advertising_platforms.Exceptions;

public class AdvertisingValidationException : AdvertisingPlatformException
{
    public AdvertisingValidationException(string message) : base(message)
    {
    }

    public AdvertisingValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
} 