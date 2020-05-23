// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthJanitor.Integrations.DataStores.AzureBlobStorage
{
    internal sealed class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return TimeSpan.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
