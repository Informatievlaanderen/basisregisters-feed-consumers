namespace Basisregisters.FeedConsumers.Console.PostalInformation;

using System;
using Model;

public sealed class PostalInformationProjector
{
    private static PostalInformationStatus MapStatus(string status)
    {
        return status switch
        {
            "gerealiseerd" => PostalInformationStatus.Realized,
            "gehistoreerd" => PostalInformationStatus.Retired,
            _ => throw new ArgumentException($"Unknown status: {status}")
        };
    }
}
