using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PasswordstateOperator.Passwordstate
{
    public class PasswordsParser
    {
        public List<Password> Parse(string json)
        {
            return (JsonSerializer.Deserialize<List<Dictionary<string, dynamic>>>(json) ?? new List<Dictionary<string, dynamic>>())
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