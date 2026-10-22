using App.Core.Services;

namespace App.Tests;

/// <summary>
/// The japa screen's arithmetic. It must agree with SadhanaService.LogCountAsync tap for tap, or the
/// screen would celebrate a mala the saved log does not have.
/// </summary>
public class JapaCounterTests
{
    [Fact]
    public void A_mala_completes_exactly_at_its_size()
    {
        var c = new JapaCounter(total: 0, malas: 0, inMala: 0, malaSize: 3);

        Assert.False(c.Tap());
        Assert.False(c.Tap());
        Assert.True(c.Tap());

        Assert.Equal(3, c.Total);
        Assert.Equal(1, c.Malas);
        Assert.Equal(0, c.InMala);
    }

    [Fact]
    public void Progress_fills_towards_the_mala()
    {
        var c = new JapaCounter(total: 54, malas: 0, inMala: 54, malaSize: 108);
        Assert.Equal(0.5, c.Progress, 3);
    }

    [Fact]
    public void A_smaller_size_carries_the_count_in_progress_and_completes_on_the_next_chant()
    {
        // 115 at 108 per mala: one done, seven in progress.
        var c = new JapaCounter(total: 115, malas: 1, inMala: 7, malaSize: 108);

        c.ChangeMalaSize(5);
        Assert.Equal(1, c.Malas);          // nothing invented by the change itself
        Assert.Equal(1d, c.Progress);      // shown full, waiting for its next chant

        Assert.True(c.Tap());              // 8 in progress against 5: one mala, three carried
        Assert.Equal(2, c.Malas);
        Assert.Equal(3, c.InMala);
        Assert.Equal(116, c.Total);
    }

    [Fact]
    public void A_larger_size_never_takes_a_mala_back()
    {
        var c = new JapaCounter(total: 22, malas: 2, inMala: 0, malaSize: 11);

        c.ChangeMalaSize(108);

        Assert.Equal(2, c.Malas);
        Assert.False(c.Tap());
        Assert.Equal(1, c.InMala);
    }

    [Fact]
    public void Reset_clears_only_the_mala_in_progress()
    {
        var c = new JapaCounter(total: 115, malas: 1, inMala: 7, malaSize: 108);

        c.ResetCurrentMala();

        Assert.Equal(108, c.Total);
        Assert.Equal(1, c.Malas);
        Assert.Equal(0, c.InMala);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(108, true)]
    [InlineData(100_000, true)]
    [InlineData(2, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(100_001, false)]
    public void A_mala_is_three_chants_or_more(int size, bool valid)
        => Assert.Equal(valid, JapaCounter.IsValidMalaSize(size));

    [Fact]
    public void A_size_below_the_floor_is_raised_to_it()
    {
        var c = new JapaCounter(total: 0, malas: 0, inMala: 0, malaSize: 1);
        Assert.Equal(JapaCounter.MinMalaSize, c.MalaSize);

        c.ChangeMalaSize(0);
        Assert.Equal(JapaCounter.MinMalaSize, c.MalaSize);
    }
}
