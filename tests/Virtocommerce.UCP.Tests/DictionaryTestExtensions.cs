using System.Collections.Generic;
using Xunit;

namespace Virtocommerce.UCP.Tests;

internal static class DictionaryTestExtensions
{
    public static IDictionary<string, object> AsDictionary(this object value)
    {
        return Assert.IsAssignableFrom<IDictionary<string, object>>(value);
    }
}
