using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PasswordstateOperator.Passwordstate
{
    public class PasswordsParser
    {
        public List<Password> Parse(string json)
        {
            var passwords = JsonSerializer.Deserialize<List<Dictionary<string, dynamic>>>(json) ?? new List<Dictionary<string, dynamic>>();

            foreach (var fields in passwords)
            {
                if (fields.TryGetValue("GenericFieldInfo", out var genericFieldInfo))
                {
                    var genericFieldInfos = JsonSerializer.Deserialize<List<Dictionary<string, dynamic>>>(genericFieldInfo.ToString()) ?? new List<Dictionary<string, dynamic>>();
                    foreach (var info in genericFieldInfos)
                    {
                        var genericFieldName = info["GenericFieldID"].ToString();
                        var displayName = info["DisplayName"].ToString();
                        var value = info["Value"];

                        fields.Remove(genericFieldName);
                        fields.Add(displayName, value);
                    }

                    fields.Remove("GenericFieldInfo");
                }
            }
                
            return passwords
                .Select(password => new Password
                {
                    Fields = password.Select(field => new Field
                    {
                        Name = field.Key,
                        Value = (field.Value ?? "").ToString()
                    }).ToList()
                })
                .ToList();
        }
    }
}