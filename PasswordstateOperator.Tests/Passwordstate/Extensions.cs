using System.Collections.Generic;
using PasswordstateOperator.Passwordstate;
using Xunit;

namespace PasswordstateOperator.Tests.Passwordstate
{
    public static class Extensions
    {
        public static void AssertEquals(this List<Password> expected, List<Password> result)
        {
            Assert.Equal(expected.Count, result.Count);

            for (var i = 0; i < result.Count; i++)
            {
                var resultingPassword = result[i];
                var expectedPassword = expected[i];

                Assert.Equal(expectedPassword.Fields.Count, resultingPassword.Fields.Count);

                for (var j = 0; j < resultingPassword.Fields.Count; j++)
                {
                    var resultingField = resultingPassword.Fields[j];
                    var expectedField = expectedPassword.Fields[j];

                    Assert.Equal(expectedField.Name, resultingField.Name);
                    Assert.Equal(expectedField.Value, resultingField.Value);
                }
            }
        }
    }
}