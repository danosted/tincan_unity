#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>What happened when a player pressed E on a station during a challenge.</summary>
    public enum ChallengeClaim
    {
        /// <summary>The station is not lit (or someone is doing it): an ordinary check.</summary>
        NotLit = 0,

        /// <summary>The player does the lit step.</summary>
        Claimed = 1,

        /// <summary>Lit, but for someone else (a relay's next player, an escalation's player).</summary>
        Refused = 2
    }

    /// <summary>A station lit by a running challenge, waiting for someone (or being done).</summary>
    public sealed class ChallengeLitStep
    {
        public ChallengeLitStep(int id, SkillCheckDefinition station, int level, float deadline, ulong? notFor, ulong? handTo, float handAt)
        {
            Id = id;
            Station = station;
            Level = level;
            Deadline = deadline;
            NotFor = notFor;
            HandTo = handTo;
            HandAt = handAt;
        }

        public int Id { get; }
        public SkillCheckDefinition Station { get; }

        /// <summary>How much harder than the station's own check (an escalation's round).</summary>
        public int Level { get; }

        /// <summary>Started by then, or it is a Miss.</summary>
        public float Deadline { get; }

        /// <summary>A relay: not this player, while the crew has others.</summary>
        public ulong? NotFor { get; }

        /// <summary>An escalation: only this player, and it opens for them at <see cref="HandAt"/>.</summary>
        public ulong? HandTo { get; }
        public float HandAt { get; }

        /// <summary>Who is doing it, once started.</summary>
        public ulong? Client { get; internal set; }
        public bool Started => Client != null;
    }

    /// <summary>
    /// Domain Layer, pure: one challenge being played. It walks the definition's step tree, lights stations
    /// (<see cref="Lit"/>), takes the grades of their checks and ends with the challenge's grade. Sequence, relay and
    /// parallel combine their steps' grades by the definition's rule; a branch picks its next step by the first's grade;
    /// an escalation repeats one station, harder each round. A step at a station the ship lacks is skipped, and a
    /// challenge whose every step is skipped ends <see cref="WasEmpty"/>. Time comes in with each call.
    /// Plan: hazards-and-boons.md (B4a).
    /// </summary>
    public sealed class SkillChallengeRun
    {
        /// <summary>Seconds between an escalation's rounds, while the last grade shows.</summary>
        public const float HandOverDelay = 1.5f;

        private readonly Func<SkillCheckDefinition, bool> _hasStation;
        private readonly List<ChallengeLitStep> _lit = new();
        private readonly Dictionary<int, LeafNode> _leaves = new();
        private int _nextId;
        private float _now;

        public SkillChallengeRun(SkillChallengeDefinition definition, Func<SkillCheckDefinition, bool> hasStation, float now)
        {
            Definition = definition;
            _hasStation = hasStation;
            _now = now;
            var root = Build(definition.Root, null);
            if (root == null) End(null);
            else root.Start(this);
        }

        public SkillChallengeDefinition Definition { get; }
        public IReadOnlyList<ChallengeLitStep> Lit => _lit;
        public bool IsDone { get; private set; }
        public SkillCheckGrade Grade { get; private set; }

        /// <summary>Ended without a single step to do (no station it names is aboard).</summary>
        public bool WasEmpty { get; private set; }

        /// <summary>Who did the last step.</summary>
        public ulong? LastClient { get; private set; }

        /// <summary>Lit stations no one started in time are a Miss.</summary>
        public void Tick(float now)
        {
            _now = now;
            foreach (var step in _lit.ToArray())
            {
                if (!step.Started && now > step.Deadline) Report(step.Id, SkillCheckGrade.Miss, null, now);
            }
        }

        /// <summary>A player pressed E on this station: do they take its lit step?</summary>
        public ChallengeClaim TryClaim(SkillCheckDefinition station, ulong client, int crew, float now, out ChallengeLitStep? claimed)
        {
            _now = now;
            claimed = _lit.Find(step => step.Station == station && !step.Started);
            if (claimed == null) return ChallengeClaim.NotLit;

            bool refused = claimed.HandTo is { } handTo ? handTo != client : claimed.NotFor == client && crew > 1;
            if (refused)
            {
                claimed = null;
                return ChallengeClaim.Refused;
            }

            claimed.Client = client;
            return ChallengeClaim.Claimed;
        }

        /// <summary>An escalation's next rounds, due to open for their player.</summary>
        public IEnumerable<ChallengeLitStep> DueHandOvers(float now)
        {
            foreach (var step in _lit)
            {
                if (!step.Started && step.HandTo != null && now >= step.HandAt) yield return step;
            }
        }

        /// <summary>A lit step's check ended (client: who did it; null when no one started it).</summary>
        public void Report(int stepId, SkillCheckGrade grade, ulong? client, float now)
        {
            _now = now;
            if (!_leaves.TryGetValue(stepId, out var leaf)) return;

            _leaves.Remove(stepId);
            _lit.RemoveAll(step => step.Id == stepId);
            if (client != null) LastClient = client;
            leaf.Complete(this, grade, client);
        }

        private void Light(LeafNode leaf)
        {
            float handAt = _now + (leaf.HandTo != null ? HandOverDelay : 0f);
            var step = new ChallengeLitStep(++_nextId, leaf.Station!, leaf.Level, handAt + Definition.StepSeconds,
                leaf.InRelay ? LastClient : null, leaf.HandTo, handAt);
            _leaves[step.Id] = leaf;
            _lit.Add(step);
        }

        private void End(SkillCheckGrade? grade)
        {
            IsDone = true;
            WasEmpty = grade == null;
            Grade = grade ?? SkillCheckGrade.Miss;
            _lit.Clear();
            _leaves.Clear();
        }

        private SkillCheckGrade? Combine(List<SkillCheckGrade> grades)
        {
            if (grades.Count == 0) return null;
            if (Definition.GradeRule == ChallengeGradeRule.Worst)
            {
                var worst = SkillCheckGrade.Perfect;
                foreach (var grade in grades) worst = grade < worst ? grade : worst;
                return worst;
            }

            float sum = 0f;
            foreach (var grade in grades) sum += (int)grade;
            return (SkillCheckGrade)(int)Math.Floor(sum / grades.Count + 0.5f);
        }

        private static Node? Build(ChallengeStep? step, Node? parent)
        {
            Node? node = step switch
            {
                StationStep station => new LeafNode(station.Station, 0, null),
                SequenceStep sequence => new ListNode(ListMode.Sequence, sequence.Steps),
                RelayStep relay => new ListNode(ListMode.Relay, relay.Steps),
                ParallelStep parallel => new ListNode(ListMode.Parallel, parallel.Steps),
                BranchStep branch => new BranchNode(branch),
                EscalateStep escalate => new EscalateNode(escalate.Station, escalate.PerfectAt),
                _ => null
            };
            if (node == null) return null;

            node.Parent = parent;
            node.BuildChildren();
            return node;
        }

        private abstract class Node
        {
            public Node? Parent;

            public bool InRelay
            {
                get
                {
                    for (var node = Parent; node != null; node = node.Parent)
                    {
                        if (node is ListNode { Mode: ListMode.Relay }) return true;
                    }
                    return false;
                }
            }

            public virtual void BuildChildren() { }

            public abstract void Start(SkillChallengeRun run);

            public virtual void ChildDone(SkillChallengeRun run, Node child, SkillCheckGrade? grade, ulong? client) { }

            protected void Finish(SkillChallengeRun run, SkillCheckGrade? grade, ulong? client)
            {
                if (Parent == null) run.End(grade);
                else Parent.ChildDone(run, this, grade, client);
            }
        }

        private sealed class LeafNode : Node
        {
            public LeafNode(SkillCheckDefinition? station, int level, ulong? handTo)
            {
                Station = station;
                Level = level;
                HandTo = handTo;
            }

            public SkillCheckDefinition? Station { get; }
            public int Level { get; }
            public ulong? HandTo { get; }

            public override void Start(SkillChallengeRun run)
            {
                if (Station == null || !run._hasStation(Station)) Finish(run, null, null);
                else run.Light(this);
            }

            public void Complete(SkillChallengeRun run, SkillCheckGrade grade, ulong? client) => Finish(run, grade, client);
        }

        private enum ListMode
        {
            Sequence,
            Relay,
            Parallel
        }

        private sealed class ListNode : Node
        {
            private readonly List<ChallengeStep> _steps;
            private readonly List<Node> _children = new();
            private readonly List<SkillCheckGrade> _grades = new();
            private int _index;
            private int _pending;

            public ListNode(ListMode mode, List<ChallengeStep> steps)
            {
                Mode = mode;
                _steps = steps;
            }

            public ListMode Mode { get; }

            public override void BuildChildren()
            {
                foreach (var step in _steps)
                {
                    var child = Build(step, this);
                    if (child != null) _children.Add(child);
                }
            }

            public override void Start(SkillChallengeRun run)
            {
                if (Mode != ListMode.Parallel)
                {
                    _index = -1;
                    Next(run);
                    return;
                }

                _pending = _children.Count;
                if (_pending == 0) Finish(run, null, null);
                foreach (var child in _children.ToArray()) child.Start(run);
            }

            public override void ChildDone(SkillChallengeRun run, Node child, SkillCheckGrade? grade, ulong? client)
            {
                if (grade is { } given) _grades.Add(given);
                if (Mode != ListMode.Parallel) Next(run);
                else if (--_pending == 0) Finish(run, run.Combine(_grades), client);
            }

            private void Next(SkillChallengeRun run)
            {
                if (++_index < _children.Count) _children[_index].Start(run);
                else Finish(run, run.Combine(_grades), run.LastClient);
            }
        }

        private sealed class BranchNode : Node
        {
            private readonly BranchStep _step;
            private Node? _first;
            private readonly Node?[] _next = new Node?[3];
            private SkillCheckGrade? _firstGrade;

            public BranchNode(BranchStep step) => _step = step;

            public override void BuildChildren()
            {
                _first = Build(_step.First, this);
                _next[(int)SkillCheckGrade.Miss] = Build(_step.OnMiss, this);
                _next[(int)SkillCheckGrade.Good] = Build(_step.OnGood, this);
                _next[(int)SkillCheckGrade.Perfect] = Build(_step.OnPerfect, this);
            }

            public override void Start(SkillChallengeRun run)
            {
                _firstGrade = null;
                if (_first == null) Finish(run, null, null);
                else _first.Start(run);
            }

            public override void ChildDone(SkillChallengeRun run, Node child, SkillCheckGrade? grade, ulong? client)
            {
                if (child != _first)
                {
                    var grades = new List<SkillCheckGrade> { _firstGrade!.Value };
                    if (grade is { } then) grades.Add(then);
                    Finish(run, run.Combine(grades), client);
                    return;
                }

                if (grade is not { } first)
                {
                    Finish(run, null, client); // the first step was skipped: so is the branch
                    return;
                }

                _firstGrade = first;
                var next = _next[(int)first];
                if (next == null) Finish(run, first, client);
                else next.Start(run);
            }
        }

        private sealed class EscalateNode : Node
        {
            private readonly SkillCheckDefinition? _station;
            private readonly int _perfectAt;
            private int _passed;

            public EscalateNode(SkillCheckDefinition? station, int perfectAt)
            {
                _station = station;
                _perfectAt = Math.Max(1, perfectAt);
            }

            public override void Start(SkillChallengeRun run)
            {
                _passed = 0;
                Round(run, null);
            }

            public override void ChildDone(SkillChallengeRun run, Node child, SkillCheckGrade? grade, ulong? client)
            {
                if (grade == null)
                {
                    Finish(run, null, client); // no such station aboard
                    return;
                }

                if (grade == SkillCheckGrade.Miss)
                {
                    Finish(run, _passed == 0 ? SkillCheckGrade.Miss : SkillCheckGrade.Good, client);
                    return;
                }

                if (++_passed >= _perfectAt) Finish(run, SkillCheckGrade.Perfect, client);
                else Round(run, client);
            }

            private void Round(SkillChallengeRun run, ulong? handTo)
            {
                var leaf = new LeafNode(_station, _passed, handTo) { Parent = this };
                leaf.Start(run);
            }
        }
    }
}
