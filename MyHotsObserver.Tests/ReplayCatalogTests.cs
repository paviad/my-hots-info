using MyHotsObserver;

namespace MyHotsObserver.Tests;

public class ReplayCatalogTests {
    [Theory]
    [InlineData(0, "1")]
    [InlineData(4, "5")]
    [InlineData(8, "9")]
    [InlineData(9, "0")]
    [InlineData(10, "")]
    public void FocusKeysRunOneToNineThenZero(int index, string key) {
        Assert.Equal(key, ReplayCatalog.FocusKey(index));
    }
}
