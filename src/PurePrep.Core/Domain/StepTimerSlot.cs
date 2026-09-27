namespace PurePrep.Domain;

/// <summary>
/// Ties a step's timer slot (recipe, step, index in the step's timer list) to the timer running for
/// it, so a renamed start still shows as running and a second tap never starts a duplicate.
/// </summary>
public static class StepTimerSlot
{
    /// <summary>
    /// The running timer for this slot: matched by <see cref="CookTimerState.TimerIndex"/>, or — for
    /// legacy timers restored without one — by <paramref name="label"/>. Null when the slot is idle.
    /// </summary>
    public static CookTimerState? FindRunning(IEnumerable<CookTimerState> live, Guid recipeId, int stepIndex, int timerIndex, string label) =>
        live.FirstOrDefault(t => t.RecipeId == recipeId && t.StepIndex == stepIndex
            && (t.TimerIndex is int index ? index == timerIndex : t.Label == label));
}
