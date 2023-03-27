// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.SemanticKernel;
public static class StringExtensions
{
    public static string ReplaceWithOptions(this string value, string oldChar, string newChar, StringComparison comparison)
    {
        string pattern = Regex.Escape(oldChar);
        RegexOptions options = RegexOptions.None;

        if (comparison == StringComparison.CurrentCultureIgnoreCase || comparison == StringComparison.InvariantCultureIgnoreCase || comparison == StringComparison.OrdinalIgnoreCase)
        {
            options = RegexOptions.IgnoreCase;
        }

        string replaced = Regex.Replace(value, pattern, newChar, options);
        return replaced;
    }

    public static bool ContainsWithComparison(this string source, string value, StringComparison comparison)
    {
        return source.IndexOf(value, comparison) >= 0;
    }
}
