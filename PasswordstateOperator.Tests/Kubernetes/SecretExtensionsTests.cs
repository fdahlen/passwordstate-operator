using System;
using System.Collections.Generic;
using System.Text;
using k8s.Models;
using PasswordstateOperator.Kubernetes;
using Xunit;

namespace PasswordstateOperator.Tests.Kubernetes
{
    public class SecretExtensionsTests
    {
        [Theory]
        [MemberData(nameof(TestCases))]
        public void DataEquals(V1Secret first, V1Secret second, bool expected)
        {
            // Act
            var result = first.DataEquals(second);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [MemberData(nameof(TestCasesTooMuchData))]
        public void DataEquals_ShouldThrow_WhenBothDataAndStringDataIsSpecified(V1Secret first, V1Secret second)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => first.DataEquals(second));
        }

        public static TheoryData<V1Secret, V1Secret, bool> TestCases =>
            new()
            {
                {
                    // Null should equal null
                    new V1Secret {StringData = null, Data = null},
                    new V1Secret {StringData = null, Data = null},
                    true
                },
                {
                    // Null should equal empty
                    new V1Secret {StringData = null, Data = null},
                    new V1Secret {StringData = null, Data = new Dictionary<string, byte[]>()},
                    true
                },
                {
                    // Null should equal empty
                    new V1Secret {StringData = null, Data = null},
                    new V1Secret {StringData = new Dictionary<string, string>(), Data = null},
                    true
                },
                {
                    // Null should equal empty
                    new V1Secret {StringData = null, Data = new Dictionary<string, byte[]>()},
                    new V1Secret {StringData = null, Data = null},
                    true
                },
                {
                    // Null should equal empty
                    new V1Secret {StringData = new Dictionary<string, string>(), Data = null},
                    new V1Secret {StringData = null, Data = null},
                    true
                },
                {
                    // Empty data should equal empty
                    new V1Secret {StringData = new Dictionary<string, string>()},
                    new V1Secret {Data = new Dictionary<string, byte[]>()},
                    true
                },
                {
                    // Empty data should equal empty
                    new V1Secret {Data = new Dictionary<string, byte[]>()},
                    new V1Secret {StringData = new Dictionary<string, string>()},
                    true
                },
                {
                    // Empty data should equal empty
                    new V1Secret {Data = new Dictionary<string, byte[]>()},
                    new V1Secret {Data = new Dictionary<string, byte[]>()},
                    true
                },
                {
                    // Empty data should equal empty
                    new V1Secret {StringData = new Dictionary<string, string>()},
                    new V1Secret {StringData = new Dictionary<string, string>()},
                    true
                },
                {
                    // Same data should equal (single entry)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}}},
                    true
                },
                {
                    // Same data should equal (single entry)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    true
                },
                {
                    // Same data should equal (single entry)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}}},
                    true
                },
                {
                    // Same data should equal (single entry)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    true
                },
                {
                    // Same data should equal (multiple entries)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}},
                    true
                },
                {
                    // Same data should equal (multiple entries)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}}},
                    true
                },
                {
                    // Same data should equal (multiple entries)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}},
                    true
                },
                {
                    // Same data should equal (multiple entries)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}}},
                    true
                },
                // TODO: test cases for should NOT equal 
                {
                    // Null should NOT equal single entry
                    new V1Secret {StringData = null},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}}},
                    false
                },
                {
                    // Null should NOT equal single entry
                    new V1Secret {Data = null},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    false
                },
                {
                    // Different data should NOT equal (single entry)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value100")}}},
                    false
                },
                {
                    // Different data should NOT equal (single entry)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value100")}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    false
                },
                {
                    // Different data should NOT equal (single entry)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value100")}}},
                    false
                },
                {
                    // Different data should NOT equal (single entry)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value100"}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}}},
                    false
                },
                {
                    // Different data should NOT equal (multiple entries)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value200")}}},
                    false
                },
                {
                    // Different data should NOT equal (multiple entries)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value200")}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}}},
                    false
                },
                {
                    // Different data should NOT equal (multiple entries)
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}},
                    new V1Secret {Data = new Dictionary<string, byte[]> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value200")}}},
                    false
                },
                {
                    // Different data should NOT equal (multiple entries)
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value200"}}},
                    new V1Secret {StringData = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}}},
                    false
                },
            };
        
        public static TheoryData<V1Secret, V1Secret> TestCasesTooMuchData =>
            new()
            {
                {
                    new V1Secret {StringData = new Dictionary<string, string>(), Data = new Dictionary<string, byte[]>()},
                    new V1Secret {StringData = null, Data = null}
                },
                {
                    new V1Secret {StringData = null, Data = null},
                    new V1Secret {StringData = new Dictionary<string, string>(), Data = new Dictionary<string, byte[]>()}
                },
                {
                    new V1Secret {StringData = new Dictionary<string, string>(), Data = new Dictionary<string, byte[]>()},
                    new V1Secret {StringData = new Dictionary<string, string>(), Data = new Dictionary<string, byte[]>()}
                },
            };
    }
}