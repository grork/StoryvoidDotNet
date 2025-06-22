using Codevoid.Storyvoid.Sync;

namespace Codevoid.Test.Storyvoid.Sync;

public class ChunkinatorTests
{
    private static readonly int[] MULTIPLE_CHUNK_SIZE_SAMPLE = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    private static readonly int[] NON_MULTIPLE_CHUNK_SIZE_SAMPLE = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12 };

    public IEnumerable<int> ExplodingAfterN(uint size, uint explodeAtIndex)
    {
        for (var index = 0; index < size; index += 1)
        {
            if (index >= explodeAtIndex)
            {
                throw new InvalidOperationException("I am bad");
            }

            yield return index;
        }
    }
    [Fact]
    public void ThrowsWithZeroChunkSize()
    {
        int[] sample = new int[] { };
        Assert.Throws<ArgumentOutOfRangeException>(() => sample.Chunkify(0));
    }

    [Fact]
    public void CheckExplodingHelperDoesntThrowUnderLimit()
    {
        Assert.Equal(5, ExplodingAfterN(10, 6).Take(5).Count());
    }

    [Fact]
    public void CheckExplodingHelperThrowsPastLimit()
    {
        Assert.Throws<InvalidOperationException>(() => ExplodingAfterN(10, 6).Take(8).Count());
    }

    [Fact]
    public void AllOriginalElementsAreReturnedForExactMultipleOfChunkSize()
    {
        var seenValues = new List<int>();
        foreach (var chunk in MULTIPLE_CHUNK_SIZE_SAMPLE.Chunk(5))
        {
            seenValues.AddRange(chunk);
        }

        Assert.Equal(MULTIPLE_CHUNK_SIZE_SAMPLE, seenValues);
    }

    [Fact]
    public void AllOriginalElementsAreReturnedInCorrectChunkSizes()
    {
        const int CHUNK_SIZE = 5;
        var seenValues = new List<int>();
        foreach (var chunk in MULTIPLE_CHUNK_SIZE_SAMPLE.Chunk(CHUNK_SIZE))
        {
            Assert.Equal(CHUNK_SIZE, chunk.Count());
            seenValues.AddRange(chunk);
        }

        Assert.Equal(MULTIPLE_CHUNK_SIZE_SAMPLE, seenValues);
    }

    [Fact]
    public void AllOriginalElementsAreReturnedForNotExactMultipleOfChunkSize()
    {
        var seenValues = new List<int>();
        foreach (var chunk in NON_MULTIPLE_CHUNK_SIZE_SAMPLE.Chunk(5))
        {
            seenValues.AddRange(chunk);
        }

        Assert.Equal(NON_MULTIPLE_CHUNK_SIZE_SAMPLE, seenValues);
    }

    [Fact]
    public void AllOriginalElementsAreReturnedWhenChunkSizeBiggerThanSourceData()
    {
        var seenValues = new List<int>();
        foreach (var chunk in MULTIPLE_CHUNK_SIZE_SAMPLE.Chunk(MULTIPLE_CHUNK_SIZE_SAMPLE.Length * 2))
        {
            seenValues.AddRange(chunk);
        }

        Assert.Equal(MULTIPLE_CHUNK_SIZE_SAMPLE, seenValues);
    }

    [Fact]
    public void LazilyEvaluated()
    {
        var chunkinator = ExplodingAfterN(10, 6).Chunkify(5).GetEnumerator();
        chunkinator.MoveNext();
        var current = chunkinator.Current;
        Assert.Equal(5, current.Count());
    }

    [Fact]
    public void LazilyEvaluatedSecondChunkThrowsException()
    {
        var chunkinator = ExplodingAfterN(10, 6).Chunkify(5).GetEnumerator();
        chunkinator.MoveNext();
        var current = chunkinator.Current;
        Assert.Equal(5, current.Count());

        Assert.Throws<InvalidOperationException>(() => chunkinator.MoveNext());
    }

    [Fact]
    public void EmptySourceEvaluatesNoItems()
    {
        var source = new List<int>();
        var seenValues = new List<int>();
        foreach (var chunk in source.Chunk(5))
        {
            Assert.Fail("Shouldn't have been evaluated");
            seenValues.AddRange(chunk);
        }

        Assert.Empty(seenValues);
    }
}