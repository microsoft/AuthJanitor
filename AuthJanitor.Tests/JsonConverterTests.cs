using System;
using System.Text.Json;
using AuthJanitor.Integrations.IdentityServices.AzureActiveDirectory;
using Xunit;

namespace AuthJanitor.Tests
{
    public class JsonConverterTests
    {

        [Fact]
        public void JsonSerializerShouldReturnStringForNumbers()
        {
            string expected = "{\"MemberInt\":\"5\",\"MemberLong\":\"1\"}";

            var writeAsString = true;
            string tokenJson = JsonSerializer.Serialize(SampleDTO(), GetSerializerOptions(writeAsString));

            Assert.Equal(expected, tokenJson);
        }

        [Fact]
        public void JsonSerializerShouldReturnNumbersForNumbers()
        {
            string expected = "{\"MemberInt\":5,\"MemberLong\":1}";

            var writeAsString = false;
            string tokenJson = JsonSerializer.Serialize(SampleDTO(), GetSerializerOptions(writeAsString));

            Assert.Equal(expected, tokenJson);
        }

        [Fact]
        public void JsonDeserializerShouldReturnNumberGivenNumber()
        {
            string jsonInput = "{\"MemberInt\":5,\"MemberLong\":1}";
            TestPOCO deserializedDTO = JsonSerializer.Deserialize<TestPOCO>(jsonInput, GetSerializerOptions(true));

            Assert.Equal(5, deserializedDTO.MemberInt);
            Assert.Equal(1L, deserializedDTO.MemberLong);

        }

        [Fact]
        public void JsonDeserializerShouldThrowGivenIncorrectValue()
        {
            string expectedExceptionMessage = "[5] is not a correct int value!";

            string jsonInput = "{\"MemberInt\":\"[5]\",\"MemberLong\":\"1\"}";
            Exception ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestPOCO>(jsonInput, GetSerializerOptions(true)));

            Assert.True(ex.InnerException is InvalidOperationException);
            Assert.Equal(expectedExceptionMessage, ex.InnerException.Message);
        }

        private JsonSerializerOptions GetSerializerOptions(bool writeAsString)
        {
            return new JsonSerializerOptions()
            {
                Converters = { new StringToIntJsonConverter(writeAsString), new StringToLongJsonConverter(writeAsString) }
            };
        }

        private TestPOCO SampleDTO()
        {
            return new TestPOCO()
            {
                MemberInt = 5,
                MemberLong = 1L
            };
        }

    }

    public class TestPOCO
    {
        public int MemberInt { get; set; }
        public long MemberLong { get; set; }
    }

}
