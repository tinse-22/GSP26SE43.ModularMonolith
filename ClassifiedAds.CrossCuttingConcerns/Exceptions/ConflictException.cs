using System;

namespace ClassifiedAds.CrossCuttingConcerns.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }

    public ConflictException(string reasonCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ReasonCode = reasonCode;
    }

    public string ReasonCode { get; }
}
