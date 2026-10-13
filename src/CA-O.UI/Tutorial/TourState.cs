using System.Collections.Generic;

namespace CAO.UI.Tutorial;

/// <summary>
/// Index and transitions of the tour. Pure state, no WinUI, fully unit-testable.
/// </summary>
public sealed class TourState
{
    private int _index = -1;
    private IReadOnlyList<TourStep> _steps = TourCatalog.Steps;

    public TourStep? Current => _index >= 0 && _index < Count ? _steps[_index] : null;

    public bool IsRunning { get; private set; }

    public bool IsFinished { get; private set; }

    public int Index => _index;

    public int Count => _steps.Count;

    public bool CanGoBack => _index > 0 && IsRunning;

    public void Start()
    {
        if (IsRunning) return;
        _steps = TourCatalog.Steps;
        _index = 0;
        IsRunning = true;
        IsFinished = false;
    }

    /// <summary>
    /// Starts a page tour over an arbitrary step list (see <c>PageTourCatalog</c>).
    /// Same transitions as <c>Start</c>; an empty list leaves the state idle.
    /// </summary>
    public void StartWith(IReadOnlyList<TourStep> steps)
    {
        if (IsRunning) return;
        if (steps is null || steps.Count == 0) return;
        _steps = steps;
        _index = 0;
        IsRunning = true;
        IsFinished = false;
    }

    public bool Next()
    {
        if (!IsRunning) return false;
        if (_index >= Count - 1)
        {
            Complete();
            return false;
        }

        _index++;
        return true;
    }

    public bool Back()
    {
        if (!IsRunning || _index <= 0) return false;
        _index--;
        return true;
    }

    public void Skip()
    {
        IsRunning = false;
        IsFinished = true;
    }

    public void Complete()
    {
        IsRunning = false;
        IsFinished = true;
    }

    public void Reset()
    {
        _index = -1;
        IsRunning = false;
        IsFinished = false;
        _steps = TourCatalog.Steps;
    }
}