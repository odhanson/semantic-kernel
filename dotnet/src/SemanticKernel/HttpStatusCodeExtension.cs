// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Microsoft.SemanticKernel;
public static class HttpStatusCodeExtension
{
    public enum ExtendedHttpStatusCode
    {
        // Existing status codes in .NET Standard 2.0
        Continue = HttpStatusCode.Continue,
        SwitchingProtocols = HttpStatusCode.SwitchingProtocols,
        OK = HttpStatusCode.OK,
        // ... Add all the existing status codes

        // Additional status codes in .NET Core 2.1
        AlreadyReported = 208,
        IMUsed = 226,
        UnprocessableEntity = 422,
        Locked = 423,
        FailedDependency = 424,
        UpgradeRequired = 426,
        PreconditionRequired = 428,
        TooManyRequests = 429,
        RequestHeaderFieldsTooLarge = 431,
        UnavailableForLegalReasons = 451,

        MisdirectedRequest = 421,

        // InsufficientStorage status code
        InsufficientStorage = 507,

        NetworkAuthenticationRequired = 511,
    }
}
