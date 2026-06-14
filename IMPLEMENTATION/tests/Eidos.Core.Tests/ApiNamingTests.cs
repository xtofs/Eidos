using Eidos.Core.OpenApi;

namespace Eidos.Core.Tests;

public class ApiNamingTests
{
    [Theory]
    [InlineData("Person", "persons")]       // regular
    [InlineData("Organization", "organizations")]
    [InlineData("Employment", "employments")]
    [InlineData("Company", "companies")]    // consonant + y -> ies
    [InlineData("Key", "keys")]             // vowel + y -> +s (not "keies")
    [InlineData("Status", "statuses")]      // sibilant -> es (not unchanged)
    [InlineData("Box", "boxes")]            // x -> es
    [InlineData("Dish", "dishes")]          // sh -> es
    [InlineData("Church", "churches")]      // ch -> es
    public void CollectionSegment_PluralizesRegularEnglish(string typeName, string expected)
        => Assert.Equal(expected, ApiNaming.CollectionSegmentName(typeName));
}
