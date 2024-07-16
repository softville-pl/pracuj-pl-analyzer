// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace ConsoleApp;

internal static class StringExtensions
{
    public static string JsonPrettify(this JsonDocument jDoc)
    {
        return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions {WriteIndented = true});
    }

    public static string JsonPrettify(this JsonElement jElem)
    {
        return JsonSerializer.Serialize(jElem, new JsonSerializerOptions {WriteIndented = true});
    }
}
