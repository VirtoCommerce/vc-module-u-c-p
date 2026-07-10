using System.Collections.Generic;
using Xunit;

namespace VirtoCommerce.UCP.Tests;

internal static class DictionaryTestExtensions
{
    public static IDictionary<string, object> AsDictionary(this object value)
    {
        return Assert.IsType<IDictionary<string, object>>(value, exactMatch: false);
    }
}
