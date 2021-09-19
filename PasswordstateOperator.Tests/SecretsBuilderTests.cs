using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PasswordstateOperator.Passwordstate;
using Xunit;

namespace PasswordstateOperator.Tests
{
    public class SecretsBuilderTests
    {
        [Fact]
        public void BuildPasswordsSecret_ShouldMapBasicProperties()
        {
            // Arrange
            var crd = new PasswordListCrd {Spec = new Spec {SecretName = "name1"}};
            var passwords = new List<Password>();
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Equal("v1", result.ApiVersion);
            Assert.Equal("Secret", result.Kind);
            Assert.Equal("name1", result.Metadata.Name);
        }

        [Fact]
        public void BuildPasswordsSecret_ShouldMapStringData_HappyCase()
        {
            // Arrange
            var crd = new PasswordListCrd {Spec = new Spec {SecretName = "name1"}};
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = "Database"
                        },
                        new()
                        {
                            Name = "ConnectionString",
                            Value = "connection;string"
                        },
                    }
                },
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = "Queues"
                        },
                        new()
                        {
                            Name = "Username",
                            Value = "user123"
                        },
                        new()
                        {
                            Name = "Password",
                            Value = "pass123"
                        },
                        new()
                        {
                            Name = "Url",
                            Value = "queue://broker"
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Equal(4, result.StringData.Count);
            Assert.Equal("connection;string", result.StringData["database.connectionstring"]);
            Assert.Equal("user123", result.StringData["queues.username"]);
            Assert.Equal("pass123", result.StringData["queues.password"]);
            Assert.Equal("queue://broker", result.StringData["queues.url"]);
        }
        
        [Fact]
        public void BuildPasswordsSecret_ShouldSkipAndLog_WhenMissingTitleField()
        {
            // Arrange
            var passwordListId = "listId";
            var crd = new PasswordListCrd
            {
                Spec = new Spec
                {
                    SecretName = "name1",
                    PasswordListId = passwordListId
                }
            };
            
            var passwordIdForEntryWithoutTitle = "id123";
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "PasswordID",
                            Value = passwordIdForEntryWithoutTitle
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Empty(result.StringData);

            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Warning, logger.Messages.Single().level);
            Assert.Contains(passwordListId, logger.Messages.Single().message);
            Assert.Contains(passwordIdForEntryWithoutTitle, logger.Messages.Single().message);
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("%&()")]
        public void BuildPasswordsSecret_ShouldSkipAndLog_WhenInvalidTitleValue(string title)
        {
            // Arrange
            var passwordListId = "listId";
            var crd = new PasswordListCrd
            {
                Spec = new Spec
                {
                    SecretName = "name1",
                    PasswordListId = passwordListId
                }
            };
            
            var passwordIdForEntryWithoutTitle = "id123";
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = title
                        },
                        new()
                        {
                            Name = "PasswordID",
                            Value = passwordIdForEntryWithoutTitle
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Empty(result.StringData);

            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Warning, logger.Messages.Single().level);
            Assert.Contains(passwordListId, logger.Messages.Single().message);
            Assert.Contains(passwordIdForEntryWithoutTitle, logger.Messages.Single().message);
        }
        
        [Fact]
        public void BuildPasswordsSecret_ShouldNotIncludeTitleFieldAsSecretKey()
        {
            // Arrange
            var crd = new PasswordListCrd {Spec = new Spec {SecretName = "name1"}};
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = "Database"
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Empty(result.StringData);
        }
        
        [Fact]
        public void BuildPasswordsSecret_ShouldNotIncludePasswordIdFieldAsSecretKey()
        {
            // Arrange
            var crd = new PasswordListCrd {Spec = new Spec {SecretName = "name1"}};
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = "Database"
                        },
                        new()
                        {
                            Name = "PasswordID",
                            Value = "123"
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Empty(result.StringData);
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("%&()")]
        public void BuildPasswordsSecret_ShouldSkipAndLog_WhenInvalidFieldName(string fieldName)
        {
            // Arrange
            var passwordListId = "listId";
            var crd = new PasswordListCrd
            {
                Spec = new Spec
                {
                    SecretName = "name1",
                    PasswordListId = passwordListId
                }
            };
            
            var passwordIdForEntryWithoutTitle = "id123";
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = "Database"
                        },
                        new()
                        {
                            Name = "PasswordID",
                            Value = passwordIdForEntryWithoutTitle
                        },
                        new()
                        {
                            Name = fieldName,
                            Value = "value123"
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Empty(result.StringData);

            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Warning, logger.Messages.Single().level);
            Assert.Contains(passwordListId, logger.Messages.Single().message);
            Assert.Contains(passwordIdForEntryWithoutTitle, logger.Messages.Single().message);
        }
        
        [Fact]
        public void BuildPasswordsSecret_ShouldSkip_WhenFieldValuesAreNullAndEmpty()
        {
            // Arrange
            var crd = new PasswordListCrd {Spec = new Spec {SecretName = "name1"}};
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = "Database"
                        },
                        new()
                        {
                            Name = "NullProperty",
                            Value = null
                        },
                        new()
                        {
                            Name = "EmptyProperty",
                            Value = ""
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Empty(result.StringData);
        }
        
        [Theory]
        [InlineData("UPPER", "CASE", "upper.case")]
        [InlineData("MiXeD", "cAsE", "mixed.case")]
        [InlineData("title.with.dots", "field.with.dots", "title.with.dots.field.with.dots")]
        [InlineData("title_with_underscore", "field_with_underscore", "title_with_underscore.field_with_underscore")]
        [InlineData("title-with-dash", "field-with-dash", "title-with-dash.field-with-dash")]
        [InlineData("123", "456", "123.456")]
        [InlineData("title with spaces", "field with spaces", "titlewithspaces.fieldwithspaces")]
        [InlineData("ÅÄÖtitle", "fieldÎÛÕ", "title.field")]
        [InlineData("invalid\"#¤@£$€§½+?`´*',%&!{}[]():;\\/", "chars", "invalid.chars")]
        public void BuildPasswordsSecret_ShouldCleanDataKeys(string title, string field, string expectedKeyName)
        {
            // Arrange
            var crd = new PasswordListCrd {Spec = new Spec {SecretName = "name1"}};
            var passwords = new List<Password>
            {
                new()
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = "Title",
                            Value = title
                        },
                        new()
                        {
                            Name = field,
                            Value = "whatever"
                        },
                    }
                },
            };
            
            var logger = new FakeLogger<SecretsBuilder>();
            var builder = new SecretsBuilder(logger);
            
            // Act
            var result = builder.BuildPasswordsSecret(crd, passwords);

            // Assert
            Assert.Single(result.StringData);
            Assert.Equal(expectedKeyName, result.StringData.Single().Key);
        }
    }
}