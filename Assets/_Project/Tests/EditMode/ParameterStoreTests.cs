using NUnit.Framework;

/// <summary><see cref="ParameterStore"/> のユニットテスト。</summary>
public class ParameterStoreTests
{
    [SetUp]    public void SetUp()    => ParameterStore.Clear();
    [TearDown] public void TearDown() => ParameterStore.Clear();

    [Test]
    public void Initially_HasPendingFalse()
    {
        Assert.IsFalse(ParameterStore.HasPending<GamePlayParameters>());
        Assert.IsNull(ParameterStore.GetPending<GamePlayParameters>());
    }

    [Test]
    public void SetThenGet_ReturnsSameParameters()
    {
        var p = new GamePlayParameters { SongId = "test_song" };
        ParameterStore.SetPending(p);

        Assert.IsTrue(ParameterStore.HasPending<GamePlayParameters>());
        Assert.AreEqual("test_song", ParameterStore.GetPending<GamePlayParameters>().SongId);
    }

    [Test]
    public void GetPending_WrongType_ReturnsNull()
    {
        ParameterStore.SetPending(new GamePlayParameters { SongId = "x" });
        Assert.IsNull(ParameterStore.GetPending<ResultParameters>());
        Assert.IsFalse(ParameterStore.HasPending<ResultParameters>());
    }

    [Test]
    public void SetTwice_OverwritesPrevious()
    {
        ParameterStore.SetPending(new GamePlayParameters { SongId = "first" });
        ParameterStore.SetPending(new ResultParameters());
        Assert.IsFalse(ParameterStore.HasPending<GamePlayParameters>());
        Assert.IsTrue(ParameterStore.HasPending<ResultParameters>());
    }

    [Test]
    public void Clear_RemovesPending()
    {
        ParameterStore.SetPending(new GamePlayParameters { SongId = "test_song" });
        ParameterStore.Clear();
        Assert.IsFalse(ParameterStore.HasPending<GamePlayParameters>());
        Assert.IsNull(ParameterStore.GetPending<GamePlayParameters>());
    }

    [Test]
    public void EmptyParameters_IsNotNull()
    {
        ParameterStore.SetPending(EmptyParameters.Instance);
        Assert.IsTrue(ParameterStore.HasPending<EmptyParameters>());
    }
}
