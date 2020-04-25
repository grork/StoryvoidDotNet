using Codevoid.Utilities.OAuth;
using Xunit;
using Xunit.Extensions.Ordering;
using Xunit.Sdk;

// Tests in this assembly are ordered due to their dependency on the service
// So we do not want them to be parallelized
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]

// We want to order thet tests more carefully to minimize runtime by being able
// to reach known state on the service (which also has limits on how many items
// can be added in a 24hr period). To do this we order tests by the functional
// area, and then by the individual tests. This attribute configures the default
// test-orderer so we don't need to specify it multiple times.

// Orders tests relative to eachother
[assembly: TestCaseOrderer("Xunit.Extensions.Ordering.TestCaseOrderer", "Xunit.Extensions.Ordering")]
// Orders classes & collections relative to eachother
[assembly: TestCollectionOrderer("Xunit.Extensions.Ordering.CollectionOrderer", "Xunit.Extensions.Ordering")]
// Enables the actual ordering of collections
[assembly: TestFramework("Xunit.Extensions.Ordering.TestFramework", "Xunit.Extensions.Ordering")]

namespace Codevoid.Test.Instapaper
{
    public static class TestUtilities
    {
        public static ClientInformation GetClientInformation()
        {
            return new ClientInformation(
                InstapaperAPIKey.CLIENT_ID,
                InstapaperAPIKey.CLIENT_SECRET,
                InstapaperAPIKey.ACCESS_TOKEN,
                InstapaperAPIKey.TOKEN_SECRET
            );
        }

        public const string TestCollectionName = "Instapaper API Tests";
    }

    // Definition only class to group API tests together, allowing for correct
    // ordering
    [CollectionDefinition(TestUtilities.TestCollectionName)]
    public class ApiTestsCollection : ICollectionFixture<CurrentServiceStateFixture>
    { }
}
