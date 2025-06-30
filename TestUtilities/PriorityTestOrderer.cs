using Xunit.Abstractions;
using Xunit.Sdk;

namespace Codevoid.Test;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class OrderAttribute : Attribute
{
    public int Order { get; }
    public OrderAttribute(int order) => Order = order;
}

/// <summary>
/// Custom that orderes both tests, and test collections by an applied `Order` attribute.
/// If there are multiple tests or collections with the same order, the order is undefined.
/// </summary>
public class PriorityTestOrderer : ITestCaseOrderer, ITestCollectionOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        return testCases.OrderBy(testCase =>
        {
            foreach (var attribute in testCase.TestMethod.Method.GetCustomAttributes(typeof(OrderAttribute)))
            {
                return attribute.GetNamedArgument<int>("Order");
            }

            return 0;
        });
    }

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections)
    {
        return testCollections.OrderBy(collection =>
        {
            // Collections might not actually have a definition, so give them priority of 0 if they don't
            if (collection.CollectionDefinition == null)
            {
                return 0;
            }

            foreach (var attribute in collection.CollectionDefinition.GetCustomAttributes(typeof(OrderAttribute)))
            {
                return attribute.GetNamedArgument<int>("Order");
            }

            return 0;
        });
    }
}
