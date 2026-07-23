using VoidDay.Core.Events;

namespace VoidDay.Balance.Sim;

/// The sim's stand-in for the player tapping "collect" on a completed quest. In-game, collection is a discrete
/// UI tap (the quest pill / menu — View) at a top-level moment, never inside an economic event cascade. The
/// sim models only the economic fact — a completed quest yields its reward — so it collects every completed
/// quest, but it must do so at the SAME discrete altitude the player does.
///
/// It therefore only RECORDS completions here (enqueue); the runner drains them and publishes M1's
/// CollectQuestRequested from the top of its loop. Collecting reentrantly — straight inside the QuestCompleted
/// dispatch — would nest a collect inside the MoneyChanged→level-up→AwardXp cascade that produced the
/// completion, and a reward's own XP can level-up→grant money→complete another quest→collect again, without
/// bound (stack overflow). Deferring to the loop keeps every collect flat and top-level, exactly as a tap is.
public sealed class QuestCollector
{
    readonly EventBus _bus;
    readonly Queue<string> _ready = new();

    public QuestCollector(EventBus bus)
    {
        _bus = bus;
        _bus.Subscribe<QuestCompleted>(e => _ready.Enqueue(e.QuestId));
    }

    /// Collect every quest that has completed since the last drain, top-level. A collect may complete further
    /// quests (its reward XP can level-up); those enqueue and this same loop drains them — each quest is
    /// collected exactly once (QuestLog retires it), so the loop always terminates.
    public void DrainCollections()
    {
        while (_ready.Count > 0)
            _bus.Publish(new CollectQuestRequested(_ready.Dequeue()));
    }
}
