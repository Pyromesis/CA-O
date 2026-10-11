using CAO.UI.Tutorial;
using Xunit;

namespace CAO.UI.Tests;

public sealed class TourStateTests
{
    private static TourState Started()
    {
        var state = new TourState();
        state.Start();
        return state;
    }

    [Fact]
    public void Fresh_state_is_not_running()
    {
        var state = new TourState();
        Assert.False(state.IsRunning);
        Assert.False(state.IsFinished);
        Assert.Null(state.Current);
        Assert.Equal(-1, state.Index);
    }

    [Fact]
    public void Start_lands_on_the_first_step()
    {
        var state = Started();
        Assert.True(state.IsRunning);
        Assert.False(state.IsFinished);
        Assert.Equal(0, state.Index);
        Assert.Equal("AnalyzeButton", state.Current!.TargetName);
    }

    [Fact]
    public void Next_advances_and_Back_returns()
    {
        var state = Started();
        Assert.True(state.Next());
        Assert.Equal(1, state.Index);
        Assert.True(state.Back());
        Assert.Equal(0, state.Index);
    }

    [Fact]
    public void Back_at_the_first_step_is_a_no_op()
    {
        var state = Started();
        Assert.False(state.Back());
        Assert.Equal(0, state.Index);
        Assert.True(state.IsRunning);
    }

    [Fact]
    public void Next_on_the_last_step_completes()
    {
        var state = Started();
        for (var i = 0; i < state.Count - 1; i++) Assert.True(state.Next());
        Assert.Equal(state.Count - 1, state.Index);
        Assert.False(state.Next());
        Assert.True(state.IsFinished);
        Assert.False(state.IsRunning);
    }

    [Fact]
    public void Skip_finishes_the_tour()
    {
        var state = Started();
        state.Skip();
        Assert.True(state.IsFinished);
        Assert.False(state.IsRunning);
    }

    [Fact]
    public void Next_and_Back_on_a_finished_tour_do_nothing()
    {
        var state = Started();
        state.Skip();
        Assert.False(state.Next());
        Assert.False(state.Back());
        Assert.True(state.IsFinished);
    }

    [Fact]
    public void Reset_returns_to_the_initial_state()
    {
        var state = Started();
        state.Next();
        state.Reset();
        Assert.Equal(-1, state.Index);
        Assert.Null(state.Current);
        Assert.False(state.IsRunning);
        Assert.False(state.IsFinished);
    }

    /// <summary>
    /// Pins the step count against the LITERAL 5, not against <c>state.Count</c> itself.
    /// <c>Next_on_the_last_step_completes</c> above walks with <c>state.Count - 1</c> as its
    /// loop bound and asserts <c>state.Count - 1</c> as the expected Index, so a regression
    /// to <c>Count => TourCatalog.Steps.Count - 1</c> would make that fact walk 3 steps,
    /// complete at index 3 and still pass. The lost step would be the last one, which is the
    /// step that tells the user their safety net exists. The final route is pinned too, so
    /// the pinned count and the pinned route cannot drift apart.
    /// </summary>
    [Fact]
    public void Count_is_five_and_the_last_step_is_restore()
    {
        var fresh = new TourState();
        Assert.Equal(5, fresh.Count);

        var state = Started();
        for (var i = 0; i < 4; i++) Assert.True(state.Next());
        Assert.Equal(4, state.Index);
        Assert.Equal("restore", state.Current!.Route);
    }

    /// <summary>
    /// Task 6 binds the Back button's IsEnabled straight to this property, so every state a
    /// user can see it in is pinned. It is false before the tour starts, false ON the first
    /// step (there is nowhere to go back to, and Back() returns false there), true at every
    /// later step including the last, and false again once the tour is over - a stale true
    /// would leave a live-looking button that silently does nothing.
    /// </summary>
    [Fact]
    public void CanGoBack_is_only_true_after_the_first_step_and_only_while_running()
    {
        var fresh = new TourState();
        Assert.False(fresh.CanGoBack);

        var state = Started();
        Assert.False(state.CanGoBack);

        Assert.True(state.Next());
        Assert.True(state.CanGoBack);

        for (var i = 0; i < 3; i++) Assert.True(state.Next());
        Assert.Equal(4, state.Index);
        Assert.True(state.CanGoBack);

        Assert.False(state.Next());
        Assert.True(state.IsFinished);
        Assert.False(state.CanGoBack);

        var skipped = Started();
        Assert.True(skipped.Next());
        Assert.True(skipped.CanGoBack);
        skipped.Skip();
        Assert.False(skipped.CanGoBack);
    }

    /// <summary>
    /// Start() is void, so a consumer gets no signal that the call was ignored. This pins the
    /// silent no-op: a second Start() while the tour is already running must preserve the
    /// current index, not rewind to the first step. This is why Task 6's relaunch affordance
    /// cannot just call Start() on a running tour.
    /// </summary>
    [Fact]
    public void Start_while_already_running_is_a_no_op_that_preserves_the_index()
    {
        var state = Started();
        Assert.True(state.Next());
        Assert.True(state.Next());
        Assert.Equal(2, state.Index);

        state.Start();

        Assert.Equal(2, state.Index);
        Assert.True(state.IsRunning);
        Assert.Same(TourCatalog.Steps[2], state.Current);
    }

    /// <summary>
    /// The relaunch path Task 6 needs. Two independent restarts are pinned: Reset() + Start()
    /// (which must land on step 1 and must actually clear the index on the way, since Start()
    /// would overwrite it either way - the intermediate asserts are what catch a Reset() that
    /// forgets _index), and a bare Start() from a skipped state, which works because IsRunning
    /// is already false so the guard does not short-circuit. Both reach step 1 with
    /// IsRunning true and IsFinished false.
    /// </summary>
    [Fact]
    public void Reset_then_Start_restarts_the_tour_on_the_first_step()
    {
        var state = Started();
        for (var i = 0; i < 4; i++) Assert.True(state.Next());
        Assert.False(state.Next());
        Assert.True(state.IsFinished);

        state.Reset();
        Assert.Equal(-1, state.Index);
        Assert.Null(state.Current);
        Assert.False(state.IsRunning);
        Assert.False(state.IsFinished);

        state.Start();
        Assert.True(state.IsRunning);
        Assert.False(state.IsFinished);
        Assert.Equal(0, state.Index);
        Assert.Same(TourCatalog.Steps[0], state.Current);

        var skipped = Started();
        Assert.True(skipped.Next());
        skipped.Skip();
        Assert.True(skipped.IsFinished);

        skipped.Start();
        Assert.True(skipped.IsRunning);
        Assert.False(skipped.IsFinished);
        Assert.Equal(0, skipped.Index);
        Assert.Same(TourCatalog.Steps[0], skipped.Current);
    }
}
