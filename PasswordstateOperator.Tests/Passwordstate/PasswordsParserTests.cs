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
                new List<PasswordsParser.PasswordItem>
                {
                    new()
                    {
                        UserName = "username"
                    }
                },
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new()
                            {
                                Name = "UserName",
                                Value = "username"
                            },
                        }
                    }
                });
        }

        [Fact]
        public void OnePassword_OneField_IntegerValue_ShouldBeParsedAsString()
        {
            ParseInternal(
                new List<PasswordsParser.PasswordItem>
                {
                    new()
                    {
                        Password = 123456
                    }
                },
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "Password", Value = "123456" },
                        }
                    }
                });
        }
        
        [Fact]
        public void OnePassword_OneField_BooleanValue_ShouldBeParsedAsString()
        {
            ParseInternal(
                new List<PasswordsParser.PasswordItem>
                {
                    new()
                    {
                        GenericField1 = false
                    }
                },
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "GenericField1", Value = "False" },
                        }
                    }
                });
        }

        [Fact]
        public void OnePassword_OneField_ShouldIgnoreCaseOfFieldNames()
        {
            ParseInternal(
                "[\n\t{\n\t\t\"uSeRnAME\": \"user\"\n\t}\n]",
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new() { Name = "UserName", Value = "user" },
                        }
                    }
                });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void OnePassword_OneField_NoValue_ShouldBeExcluded(string username)
        {
            ParseInternal(
                new List<PasswordsParser.PasswordItem>
                {
                    new()
                    {
                        UserName = username
                    }
                },
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>()
                    }
                });
        }

        [Fact]
        public void SeveralPasswords_SeveralFields_MixedValues()
        {
            ParseInternal(
                new List<PasswordsParser.PasswordItem>
                {
                    new()
                    {
                        UserName = "Username",
                        Password = 123456,
                        AllowExport = true
                    },
                    new()
                    {
                        Password = -123456,
                        AllowExport = false
                    },
                },
                new List<Password>
                {
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new()
                            {
                                Name = "UserName",
                                Value = "Username"
                            },
                            new()
                            {
                                Name = "Password",
                                Value = "123456"
                            },
                            new()
                            {
                                Name = "AllowExport",
                                Value = "True"
                            },
                        }
                    },
                    new()
                    {
                        Fields = new List<Field>
                        {
                            new()
                            {
                                Name = "Password",
                                Value = "-123456"
                            },
                            new()
                            {
                                Name = "AllowExport",
                                Value = "False"
                            },
                        }
                    },                    
                });
        }

        [Fact]
        public void NoPasswords_ShouldBeEmptyList()
        {
            ParseInternal(
                "[]",
                new List<Password>());
        }

        [Fact]
        public void OnePassword_OneGenericField_StringValue_ShouldUseDisplayName()
        {
            ParseInternal(
                new List<PasswordsParser.PasswordItem>
                {
                    new()
                    {
                        GenericField1 = "Value",
                        GenericFieldInfo = new List<PasswordsParser.GenericFieldInfoItem>
                        {
                            new()
                            {
                                GenericFieldID = "GenericField1",
                                DisplayName = "ConnectionString"
                            }
                        }
                    },
                },
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

        private static void ParseInternal(List<PasswordsParser.PasswordItem> input, List<Password> expected)
        {
            var json = JsonSerializer.Serialize(input);
            
            ParseInternal(json, expected);
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