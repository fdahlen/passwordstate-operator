using System.Collections.Generic;

namespace PasswordstateOperator.Passwordstate
{
    public class PasswordListResponse
    {
        public List<Password> Passwords { get; init; }
        public string Json { get; init; }
    }

    public class Password
    {
        public List<Field> Fields { get; init; }
    }

    public class Field
    {
        public string Name { get; init; }
        public string Value { get; init; }
    }
}