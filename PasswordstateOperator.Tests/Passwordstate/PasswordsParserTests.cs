using System.Collections.Generic;
using System.Text.Json;
using PasswordstateOperator.Passwordstate;
using Xunit;

namespace PasswordstateOperator.Tests.Passwordstate
{
    public class PasswordsParserTests
    {
        [Fact]
        public void OnePassword_OneField_StringValue()
        {
            ParseInternal(
                "[\n\t{\n\t\t\"StringField\": \"Value\"\n\t}\n]", 
                new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new() { Name = "StringField", Value = "Value" },
                    }
                }
            });
        }
        
        [Fact]
        public void OnePassword_OneField_IntegerValue()
        {
            ParseInternal(
                "[\n\t{\n\t\t\"IntegerField\": 123\n\t}\n]", 
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "IntegerField", Value = "123" },
                        }
                    }
                });
        }
        
        [Fact]
        public void OnePassword_OneField_NullValue_ShouldBeEmptyString()
        {
            ParseInternal(
                "[\n\t{\n\t\t\"NullField\": null\n\t}\n]", 
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "NullField", Value = "" },
                        }
                    }
                });
        }
        
        [Fact]
        public void SeveralPasswords_SeveralFields_MixedValues()
        {
            ParseInternal(
                "[\n\t{\n\t\t\"StringField1\": \"Value1\",\n\t\t\"StringField2\": \"Value2\",\n\t\t\"IntegerField\": 123,\n\t\t\"BooleanField\": true\n\t},\n\t{\n\t\t\"StringField1\": \"Value10\",\n\t\t\"IntegerField\": -123,\n\t\t\"BooleanField\": false\n\t},\n\t{\n\t\t\"String Field 3\": \"Value 3\"\n\t}\n]", 
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "StringField1", Value = "Value1" },
                            new() { Name = "StringField2", Value = "Value2" },
                            new() { Name = "IntegerField", Value = "123" },
                            new() { Name = "BooleanField", Value = "True" },
                        }
                    },
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "StringField1", Value = "Value10" },
                            new() { Name = "IntegerField", Value = "-123" },
                            new() { Name = "BooleanField", Value = "False" },
                        }
                    },
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "String Field 3", Value = "Value 3" },
                        }
                    },
                });
        }
        
        [Fact]
        public void NoPasswords()
        {
            ParseInternal(
                "[]",
                new List<Password>());
        }

        [Fact]
        public void OnePassword_OneGenericField_StringValue_ShouldUseDisplayNameInsteadOfGeneric()
        {
            ParseInternal(
                "[\n\t{\n\t\t\"GenericField1\": \"Value\",\n\t\t\"GenericFieldInfo\": [\n\t\t\t{\n\t\t\t\t\"GenericFieldID\": \"GenericField1\",\n\t\t\t\t\"DisplayName\": \"ConnectionString\",\n\t\t\t\t\"Value\": \"Value\"\n\t\t\t}\n\t\t]\n\t}\n]", 
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "ConnectionString", Value = "Value" },
                        }
                    }
                });
        }
        
        [Fact]
        public void MalformedJson_ShouldThrow()
        {
            // Arrange
            var json = "[some unknown data]"; 
            var parser = new PasswordsParser();

            // Act & Assert
            Assert.Throws<JsonException>(() => parser.Parse(json));
        }

        private static void ParseInternal(string json, List<Password> expected)
        {
            // Arrange
            var parser = new PasswordsParser();

            // Act
            var result = parser.Parse(json);

            // Assert
            expected.AssertEquals(result);
        }
    }
}