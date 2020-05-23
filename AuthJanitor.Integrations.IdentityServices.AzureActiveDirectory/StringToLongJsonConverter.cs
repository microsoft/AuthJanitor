// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthJanitor.Integrations.IdentityServices.AzureActiveDirectory
{
    public class StringToLongJsonConverter : JsonConverter<long>
    {
        private readonly bool _writeValueAsString;

        public StringToLongJsonConverter(bool writeValueAsString)
        {
            _writeValueAsString = writeValueAsString;
        }

        /// <summary>
        ///     Reads long value from quoted string
        /// </summary>
        public override long Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
                if (Utf8Parser.TryParse(span, out long number, out var bytesConsumed) && span.Length == bytesConsumed)
                    return number;

                var strNumber = reader.GetString();
                if (long.TryParse(strNumber, out number))
                    return number;

                throw new InvalidOperationException($"{strNumber} is not a correct long value!")
                {
                    /*
                     * Here is a source from internal const System.Text.Json.ThrowHelper.ExceptionSourceValueToRethrowAsJsonException
                     * to restore a detailed info about current json context (line number and path)
                     */
                    Source = "System.Text.Json.Rethrowable"
                };
            }

            return reader.GetInt64();
        }

        /// <summary>
        ///     Writes a long value as number if <see cref="_writeValueAsString" /> is false
        ///     or as string if <see cref="_writeValueAsString" /> is true
        /// </summary>
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            if (_writeValueAsString)
                writer.WriteStringValue(value.ToString());
            else
                writer.WriteNumberValue(value);
        }
    }
}
