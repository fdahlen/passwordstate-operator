using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PasswordstateOperator.Passwordstate
{
    public class PasswordsParser
    {
        public List<Password> Parse(string json)
        {
            var list = Deserialize(json)
                .Select(passwordItem => new Password
                {
                    Fields = new List<Field>
                    {
                        new()
                        {
                            Name = nameof(passwordItem.PasswordID),
                            Value = passwordItem.PasswordID?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.Title),
                            Value = passwordItem.Title?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.Domain),
                            Value = passwordItem.Domain?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.HostName),
                            Value = passwordItem.HostName?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.UserName),
                            Value = passwordItem.UserName?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.Description),
                            Value = passwordItem.Description?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.AccountTypeID),
                            Value = passwordItem.AccountTypeID?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.Notes),
                            Value = passwordItem.Notes?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.URL),
                            Value = passwordItem.URL?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.Password),
                            Value = passwordItem.Password?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.ExpiryDate),
                            Value = passwordItem.ExpiryDate?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.AllowExport),
                            Value = passwordItem.AllowExport?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.AccountType),
                            Value = passwordItem.AccountType?.ToString()
                        },
                        new()
                        {
                            Name = nameof(passwordItem.OTP),
                            Value = passwordItem.OTP?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField1)),
                            Value = passwordItem.GenericField1?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField2)),
                            Value = passwordItem.GenericField2?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField3)),
                            Value = passwordItem.GenericField3?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField4)),
                            Value = passwordItem.GenericField4?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField5)),
                            Value = passwordItem.GenericField5?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField6)),
                            Value = passwordItem.GenericField6?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField7)),
                            Value = passwordItem.GenericField7?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField8)),
                            Value = passwordItem.GenericField8?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField9)),
                            Value = passwordItem.GenericField9?.ToString()
                        },
                        new()
                        {
                            Name = GetDisplayName(passwordItem.GenericFieldInfo, nameof(passwordItem.GenericField10)),
                            Value = passwordItem.GenericField10?.ToString()
                        },
                    }
                })
                .ToList();
                
                list.ForEach(password => password.Fields.RemoveAll(field => string.IsNullOrEmpty(field.Value)));

                return list;
        }
        private static List<PasswordItem> Deserialize(string json)
        {
            var ignoreCaseOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            return JsonSerializer.Deserialize<List<PasswordItem>>(json, ignoreCaseOptions) ?? new List<PasswordItem>();
        }

        private static string GetDisplayName(List<GenericFieldInfoItem> genericFieldInfos, string genericFieldName)
        {
            return genericFieldInfos?.FirstOrDefault(info => info.GenericFieldID == genericFieldName)?.DisplayName ?? genericFieldName;
        }

        public class PasswordItem
        {
            public object PasswordID { get; set; }
            public object Title { get; set; }
            public object Domain { get; set; }
            public object HostName { get; set; }
            public object UserName { get; set; }
            public object Description { get; set; }
            public object GenericField1 { get; set; }
            public object GenericField2 { get; set; }
            public object GenericField3 { get; set; }
            public object GenericField4 { get; set; }
            public object GenericField5 { get; set; }
            public object GenericField6 { get; set; }
            public object GenericField7 { get; set; }
            public object GenericField8 { get; set; }
            public object GenericField9 { get; set; }
            public object GenericField10 { get; set; }
            public List<GenericFieldInfoItem> GenericFieldInfo { get; set; }
            public object AccountTypeID { get; set; }
            public object Notes { get; set; }
            public object URL { get; set; }
            public object Password { get; set; }
            public object ExpiryDate { get; set; }
            public object AllowExport { get; set; }
            public object AccountType { get; set; }
            public object OTP { get; set; }
        }

        public class GenericFieldInfoItem
        {
            public string GenericFieldID { get; set; }
            public string DisplayName { get; set; }
        }
    }
}