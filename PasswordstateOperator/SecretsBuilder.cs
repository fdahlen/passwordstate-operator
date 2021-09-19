using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using k8s.Models;
using Microsoft.Extensions.Logging;
using PasswordstateOperator.Passwordstate;

namespace PasswordstateOperator
{
    public class SecretsBuilder
    {
        private readonly ILogger<SecretsBuilder> logger;

        public SecretsBuilder(ILogger<SecretsBuilder> logger)
        {
            this.logger = logger;
        }

        public V1Secret BuildPasswordsSecret(PasswordListCrd crd, List<Password> passwords)
        {
            var flattenedPasswords = new Dictionary<string, string>();

            foreach (var password in passwords)
            {
                const string TitleField = "Title";
                const string PasswordIdField = "PasswordID";

                var title = password.Fields.FirstOrDefault(field => field.Name == TitleField);
                if (title == null)
                {
                    var passwordId = password.Fields.FirstOrDefault(field => field.Name == PasswordIdField)?.Value;
                    logger.LogWarning($"{nameof(BuildPasswordsSecret)}: {crd.Id}: No {TitleField} found, skipping password ID {passwordId} in list ID {crd.Spec.PasswordListId}");
                    continue;
                }

                var cleanedTitle = Clean(title.Value);
                if (string.IsNullOrEmpty(cleanedTitle))
                {
                    var passwordId = password.Fields.FirstOrDefault(field => field.Name == PasswordIdField)?.Value;
                    logger.LogWarning($"{nameof(BuildPasswordsSecret)}: {crd.Id}: Invalid {TitleField} value found '{title.Value}', skipping password ID {passwordId} in list ID {crd.Spec.PasswordListId}");
                    continue;
                }

                foreach (var field in password.Fields)
                {
                    if (field.Name == TitleField || field.Name == PasswordIdField)
                    {
                        continue;
                    }

                    var cleanedFieldName = Clean(field.Name);
                    if (string.IsNullOrEmpty(cleanedFieldName))
                    {
                        var passwordId = password.Fields.FirstOrDefault(f => f.Name == PasswordIdField)?.Value;
                        logger.LogWarning($"{nameof(BuildPasswordsSecret)}: {crd.Id}: Invalid field name '{field.Name}' found, skipping password ID {passwordId} in list ID {crd.Spec.PasswordListId}");
                        continue;
                    }

                    if (string.IsNullOrEmpty(field.Value))
                    {
                        continue;
                    }

                    var key = $"{cleanedTitle}.{cleanedFieldName}";
                    flattenedPasswords[key] = field.Value;
                }
            }

            return new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta(name: crd.Spec.SecretName),
                StringData = flattenedPasswords
            };
        }

        private static string Clean(string secretKey)
        {
            return Regex.Replace(secretKey ?? "", "[^A-Za-z0-9_.-]", "").ToLower();
        }
    }
}