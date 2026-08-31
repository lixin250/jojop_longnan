using System;
using System.Collections.Generic;
using JojoP.Config;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    public enum BrothersFlow
    {
        Idle,
        Hub,
        BreakRest,
        Battling,
        RoguePick,
        Sanpang,
        Desheng
    }

    /// <summary>
    /// 我和我的龙兄南弟会话：日历 → 割草 → 得胜/散旁 → 肉鸽/休整。
    /// </summary>
    public sealed class BrothersSessionController : MonoBehaviour
    {
        public event Action FlowChanged;
        public event Action BattleHudChanged;

        public MetaProgress Meta { get; private set; }
        public RunState Run { get; private set; }
        public BrothersFlow Flow { get; private set; } = BrothersFlow.Idle;
        public SettleKind LastSettle { get; private set; }
        public string LastSettleDetail { get; private set; } = "";
        public string NextNodeHint { get; private set; } = "";
        public string LastRogueResult { get; private set; } = "";
        public IReadOnlyList<RogueOption> CurrentRogue => _rogue;
        public BrothersBattleController Battle => _battle;

        BrothersBattleController _battle;
        readonly RunRewardSystem _rewardSystem = new RunRewardSystem();
        readonly List<RogueOption> _rogue = new List<RogueOption>();
        int _rewardTrain;
        int _rewardFavor;
        int _rewardRenown;

        public void Bootstrap()
        {
            if (!CfgTables.TryLoad())
                Debug.LogWarning("[JojoP] CfgTables 未加载，兄弟团将回退旧 GameTables（技能表驱动不可用）");
            else
                RoleCatalog.Rebuild();

            Meta = new MetaProgress();
            Meta.Load();

            if (_battle == null)
            {
                var go = new GameObject("BrothersBattle");
                go.transform.SetParent(transform, false);
                _battle = go.AddComponent<BrothersBattleController>();
                _battle.Bootstrap();
                _battle.EnemyKilled += n =>
                {
                    if (Run == null) return;
                    Run.KillsThisWave += n;
                    Run.KillsThisRun += n;
                    Meta?.AddKills(n);
                    BattleHudChanged?.Invoke();
                };
                _battle.WaveCleared += OnWaveCleared;
                _battle.SanpangTriggered += OnSanpang;
                _battle.HudDirty += () => BattleHudChanged?.Invoke();
            }

            SetFlow(BrothersFlow.Hub);
        }

        public bool CanStartRun(ChapterId chapter)
        {
            return Meta != null && Meta.IsChapterUnlocked(chapter) && Meta.Stamina >= GameTables.StaminaPerRun;
        }

        public bool TryStartRun(ChapterId chapter)
        {
            if (Meta == null) Bootstrap();
            if (!Meta.IsChapterUnlocked(chapter)) return false;
            if (!Meta.TrySpendStamina()) return false;

            Run = new RunState();
            Run.Bootstrap(chapter, Meta);
            Run.RefreshJobSkillLocks();
            Meta.NoteGrade(Run.GradeYear);

            if (Run.IsBreakPhase)
            {
                EnterBreak();
                return true;
            }

            BeginBattleWave();
            return true;
        }

        public void ReturnToHub()
        {
            _battle?.Stop();
            Run = null;
            SetFlow(BrothersFlow.Hub);
        }

        public void RetryAfterSanpang()
        {
            // 局外保留，重开本章 run（再花体力）
            var chapter = Run?.Chapter ?? ChapterId.Primary;
            Run = null;
            SetFlow(BrothersFlow.Hub);
            if (CanStartRun(chapter))
                TryStartRun(chapter);
        }

        void BeginBattleWave()
        {
            if (Run == null) return;
            Run.CurrentSceneId = Run.PickScene();
            Run.WaveInNode++;
            Run.KillsThisWave = 0;

            float diff = 0.55f + (Run.GradeYear - 1) * 0.12f + Run.WaveInNode * 0.04f;
            if (Run.Chapter == ChapterId.Society)
                diff = 1.0f + Run.SocietyYearIndex * 0.12f;

            int enemies = 8 + Run.GradeYear * 2 + Run.WaveInNode;
            if (Run.Chapter == ChapterId.Primary && Run.GradeYear <= 2)
                enemies = 6 + Run.WaveInNode;

            SetFlow(BrothersFlow.Battling);
            if (Run.WaveInNode <= 1)
                LocalSaveStore.Dump("进入战斗");
            _battle.StartWave(Run, Meta, enemies, diff);
            BattleHudChanged?.Invoke();
        }

        void OnWaveCleared()
        {
            if (Run == null) return;
            _battle.SyncDeadBrothersToRun(Run, Meta);
            Run.AdvanceJoinPenalty();
            // 得胜喘气：存活兄弟回一点血，避免残血滚雪球散旁
            foreach (var b in Run.Squad)
            {
                if (!b.CanFight) continue;
                b.Hp = Mathf.Min(b.MaxHp, b.Hp + b.MaxHp * 0.35f);
            }

            _rewardTrain = 2 + Run.GradeYear;
            _rewardFavor = Run.Chapter == ChapterId.Society ? 2 : 0;
            _rewardRenown = Run.Chapter >= ChapterId.High ? 1 : 0;
            if (Run.Chapter == ChapterId.Society)
                _rewardTrain += 2;

            Meta.AddTrain(_rewardTrain);
            if (_rewardFavor > 0) Meta.AddFavor(_rewardFavor);
            if (_rewardRenown > 0) Meta.AddRenown(_rewardRenown);

            // 先预告下一节点（Advance 前）
            NextNodeHint = PreviewNextHint();
            LastSettle = SettleKind.Desheng;
            LastSettleDetail =
                $"击杀 {Run.KillsThisWave} · 培养点+{_rewardTrain}" +
                (_rewardFavor > 0 ? $" · 人情+{_rewardFavor}" : "") +
                (_rewardRenown > 0 ? $" · 声望+{_rewardRenown}" : "");

            SetFlow(BrothersFlow.Desheng);
        }

        string PreviewNextHint()
        {
            if (Run.Chapter == ChapterId.Society)
                return "下一年过年前夜";
            if (Run.Phase == CalendarPhase.UpperTerm)
                return "寒假休整";
            if (Run.Phase == CalendarPhase.WinterBreak)
                return "下学期开战";
            if (Run.Phase == CalendarPhase.LowerTerm)
                return "暑假休整";
            return "下一学年 / 下一章";
        }

        public void ConfirmDesheng()
        {
            if (Run == null)
            {
                SetFlow(BrothersFlow.Hub);
                return;
            }

            // 学期波打完：先三选一，再推进日历（休整/下学期）
            if (Run.Chapter == ChapterId.Society)
            {
                Run.AdvanceAfterDesheng(out _);
                Run.RefreshJobSkillLocks();
                RollRewardPage(reroll: false);
                SetFlow(BrothersFlow.RoguePick);
                return;
            }

            if (Run.Phase == CalendarPhase.UpperTerm || Run.Phase == CalendarPhase.LowerTerm)
            {
                RollRewardPage(reroll: false);
                SetFlow(BrothersFlow.RoguePick);
                return;
            }

            ContinueAfterRogueOrBreak();
        }

        /// <summary>肉鸽选完后：开战或进入假期。</summary>
        void ContinueCalendarAfterRogue()
        {
            if (Run == null)
            {
                SetFlow(BrothersFlow.Hub);
                return;
            }

            if (Run.Chapter == ChapterId.Society)
            {
                BeginBattleWave();
                return;
            }

            if (Run.Phase == CalendarPhase.UpperTerm)
            {
                Run.Phase = CalendarPhase.WinterBreak;
                EnterBreak();
                return;
            }

            if (Run.Phase == CalendarPhase.LowerTerm)
            {
                Run.Phase = CalendarPhase.SummerBreak;
                EnterBreak();
                return;
            }

            BeginBattleWave();
        }

        void EnterBreak()
        {
            Run.AdvanceBreakHealing();
            LastSettleDetail = "假期休整：养伤推进，可看广告赚培养点";
            NextNodeHint = Run.Phase == CalendarPhase.WinterBreak ? "之后下学期" : "之后下一学年";
            SetFlow(BrothersFlow.BreakRest);
        }

        public void FinishBreakAndContinue()
        {
            if (Run == null)
            {
                SetFlow(BrothersFlow.Hub);
                return;
            }

            if (Run.Phase == CalendarPhase.WinterBreak)
            {
                Run.Phase = CalendarPhase.LowerTerm;
                Run.CurrentSceneId = Run.PickScene();
                BeginBattleWave();
                return;
            }

            if (Run.Phase == CalendarPhase.SummerBreak)
            {
                Run.GradeYear++;
                Meta.NoteGrade(Run.GradeYear);
                var ch = Run.ChapterDef;
                if (Run.GradeYear > ch.MaxGradeYears)
                {
                    var next = (ChapterId)Mathf.Min(5, (int)Run.Chapter + 1);
                    Meta.UnlockChapter(next);
                    Meta.AddTrain(8);
                    Meta.AddRenown(3);
                    ReturnToHub();
                    return;
                }

                // 竖切：小学打完小2 也可回大厅（表仍留到小6）
                if (Run.Chapter == ChapterId.Primary && Run.GradeYear > 2)
                {
                    Meta.UnlockChapter(ChapterId.Middle);
                    Meta.AddTrain(5);
                    ReturnToHub();
                    return;
                }

                Run.Phase = CalendarPhase.UpperTerm;
                Run.CurrentSceneId = Run.PickScene();
                BeginBattleWave();
                return;
            }

            BeginBattleWave();
        }

        void ContinueAfterRogueOrBreak()
        {
            BeginBattleWave();
        }

        public void PickRogue(int index)
        {
            if (index < 0 || index >= _rogue.Count || Run == null) return;
            LastRogueResult = _rewardSystem.Apply(_rogue[index], Run, Meta);
            _rogue.Clear();
            ContinueCalendarAfterRogue();
        }

        public bool TryRewardedReroll()
        {
            if (Run == null || Flow != BrothersFlow.RoguePick || !Run.TryUseRewardReroll())
                return false;
            RollRewardPage(reroll: true, resetCounter: false);
            FlowChanged?.Invoke();
            return true;
        }

        public bool TryRewardedExpand()
        {
            if (Run == null) return false;
            bool changed = Run.TryAddAdSlot(Meta, out var message);
            LastRogueResult = message;
            if (changed) FlowChanged?.Invoke();
            return changed;
        }

        public bool CanRewardedReroll =>
            Run != null && Flow == BrothersFlow.RoguePick &&
            Run.RewardRerollsUsedOnPage < Run.RewardedRerollLimit;

        public bool CanRewardedExpand =>
            Run != null && !Run.HasUnlimitedSlots &&
            Run.AdExtraSlotsUsed < Run.AdExtraSlotLimit;

        void RollRewardPage(bool reroll, bool resetCounter = true)
        {
            if (Run == null) return;
            if (resetCounter) Run.ResetRewardPage();
            _rogue.Clear();
            _rogue.AddRange(_rewardSystem.RollThree(Run, reroll));
        }

        void OnSanpang()
        {
            if (Run == null) return;
            _battle.SyncDeadBrothersToRun(Run, Meta);
            Meta.AddTrain(1); // 安慰奖
            LastSettle = SettleKind.Sanpang;
            LastSettleDetail = $"打到 {Run.PhaseLabel()} · 出局：{string.Join("、", Run.OutList)}";
            NextNodeHint = "重玩本局（局外养成保留）";
            SetFlow(BrothersFlow.Sanpang);
        }

        public static string ShareText(SettleKind kind)
        {
            return kind == SettleKind.Sanpang
                ? "我们散旁了，你能打到啥时候？"
                : "得胜了，龙南这边还稳。";
        }

        void SetFlow(BrothersFlow flow)
        {
            Flow = flow;
            FlowChanged?.Invoke();
        }

        void OnDestroy()
        {
            if (_battle != null)
            {
                _battle.Stop();
                Destroy(_battle.gameObject);
            }
        }
    }
}
