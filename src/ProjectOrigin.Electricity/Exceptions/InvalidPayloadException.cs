
using System;

namespace ProjectOrigin.Electricity.Exceptions;

public class InvalidPayloadException : Exception
{
    public InvalidPayloadException(string message, Exception? ex = null) : base(message, ex)
    {
    }
}
