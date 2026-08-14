using System;
using System.Text;
using JojoP.Gameplay.Brothers;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.UI
{
    /// <summary>
    /// 我和我的龙兄南弟：大厅 / 战斗 HUD / 得胜·散旁 / 肉鸽 / 假期。
    /// </summary>
    public sealed class BrothersModeView : MonoBehaviour
    {
        BrothersSessionController _session;
        GameObject _hub;
        GameObject _battleHud;
        GameObject _overlay;
        Text _metaText;
        Text _chapterText;
        Text _battleInfo;
        Text _overlayTitle;
        Text _overlayBody;
        Text _overlayShare;
        readonly Button[] _rogueBtns = new Button[3];
        readonly Text[] _rogueLabels = new Text[3];
        Button _btnConfirm;
        Button _btnRetry;
        Button _btnHome;
        Button _btnShare;
        Button _btnBreakContinue;
        Button _btnAdTrain;
        Button _btnAdReroll;
        Button _btnAdExpand;
        Action _onHome;
        Action<Action> _onRewardedAd;
        ChapterId _selectedChapter = ChapterId.Primary;

        public static BrothersModeView Create(Transform canvasRoot)
        {
            var go = new GameObject("BrothersMode", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            BrothersUiUtil.Stretch(go.GetComponent<RectTransform>());
            var view = go.AddComponent<BrothersModeView>();
            view.Build();
            return view;
        }

        public void Bind(BrothersSessionController session, Action onHome, Action<Action> onRewardedAd)
        {
            _session = session;
            _onHome = onHome;
            _onRewardedAd = onRewardedAd;
            if (_session != null)
            {
                _session.FlowChanged -= Refresh;
                _session.BattleHudChanged -= RefreshBattle;
                _session.FlowChanged += Refresh;
                _session.BattleHudChanged += RefreshBattle;
            }

            Refresh();
        }

        void OnDestroy()
        {
            if (_session == null) return;
            _session.FlowChanged -= Refresh;
            _session.BattleHudChanged -= RefreshBattle;
        }

        void Build()
        {
            BuildHub();
            BuildBattleHud();
            BuildOverlay();
            ShowOnlyHub();
        }

        void BuildHub()
        {
            _hub = new GameObject("Hub", typeof(RectTransform));
            _hub.transform.SetParent(transform, false);
            BrothersUiUtil.Stretch(_hub.GetComponent<RectTransform>());

            BrothersUiUtil.MakeText(_hub.transform, "Title", "我和我的龙兄南弟", 56, new Vector2(0, 700), new Vector2(900, 80));
            _metaText = BrothersUiUtil.MakeText(_hub.transform, "Meta", "", 28, new Vector2(0, 580), new Vector2(980, 120));
            _chapterText = BrothersUiUtil.MakeText(_hub.transform, "Chapter", "章节：小学", 30, new Vector2(0, 450), new Vector2(900, 50));

            float y = 300f;
            foreach (var ch in GameTables.Chapters)
            {
                var id = ch.Id;
                var btn = BrothersUiUtil.MakeButton(_hub.transform, "Ch_" + id, ch.DisplayName, new Vector2(0, y), new Vector2(640, 72));
                btn.onClick.AddListener(() =>
                {
                    _selectedChapter = id;
                    RefreshHubTexts();
                });
                y -= 84f;
            }

            var start = BrothersUiUtil.MakeButton(_hub.transform, "BtnStart", "出征（耗体力）", new Vector2(0, -420), new Vector2(520, 100),
                new Color(0.2f, 0.7f, 0.45f));
            start.onClick.AddListener(OnStartRun);

            var potHp = BrothersUiUtil.MakeButton(_hub.transform, "BtnPotHp", "潜力生命 3点", new Vector2(-220, -540), new Vector2(280, 70));
            potHp.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyPotentialHp();
                RefreshHubTexts();
            });
            var potAtk = BrothersUiUtil.MakeButton(_hub.transform, "BtnPotAtk", "潜力攻击 3点", new Vector2(220, -540), new Vector2(280, 70));
            potAtk.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyPotentialAtk();
                RefreshHubTexts();
            });
            var heal = BrothersUiUtil.MakeButton(_hub.transform, "BtnHeal", "养伤缩短 5点", new Vector2(0, -620), new Vector2(360, 70));
            heal.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyHealTier();
                RefreshHubTexts();
            });

            var adSta = BrothersUiUtil.MakeButton(_hub.transform, "BtnAdSta", "广告+体力", new Vector2(-220, -720), new Vector2(280, 70),
                new Color(0.75f, 0.45f, 0.2f));
            adSta.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddStamina(2);
                RefreshHubTexts();
            }));
            var adTrain = BrothersUiUtil.MakeButton(_hub.transform, "BtnAdTrain", "广告+培养点", new Vector2(220, -720), new Vector2(280, 70),
                new Color(0.75f, 0.45f, 0.2f));
            adTrain.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddTrain(3);
                RefreshHubTexts();
            }));

            var home = BrothersUiUtil.MakeButton(_hub.transform, "BtnBack", "返回主界面", new Vector2(0, -820), new Vector2(360, 70),
                new Color(0.35f, 0.35f, 0.4f));
            home.onClick.AddListener(() => _onHome?.Invoke());
        }

        void BuildBattleHud()
        {
            _battleHud = new GameObject("BattleHud", typeof(RectTransform));
            _battleHud.transform.SetParent(transform, false);
            BrothersUiUtil.Stretch(_battleHud.GetComponent<RectTransform>());

            _battleInfo = BrothersUiUtil.MakeText(_battleHud.transform, "Info", "", 28, new Vector2(0, 820), new Vector2(1000, 140));
            var tip = BrothersUiUtil.MakeText(_battleHud.transform, "Tip", "自动割草中…", 24, new Vector2(0, 720), new Vector2(800, 40));
            tip.color = new Color(0.85f, 0.85f, 0.85f);

            var quit = BrothersUiUtil.MakeButton(_battleHud.transform, "BtnQuit", "弃战回大厅", new Vector2(0, -860), new Vector2(320, 64),
                new Color(0.4f, 0.25f, 0.25f));
            quit.onClick.AddListener(() => _session?.ReturnToHub());
        }

        void BuildOverlay()
        {
            var panel = BrothersUiUtil.MakePanel(transform, "Overlay", new Vector2(920, 1200), Vector2.zero, new Color(0.08f, 0.1f, 0.14f, 0.96f));
            _overlay = panel.gameObject;

            _overlayTitle = BrothersUiUtil.MakeText(_overlay.transform, "Title", "", 52, new Vector2(0, 480), new Vector2(860, 80));
            _overlayBody = BrothersUiUtil.MakeText(_overlay.transform, "Body", "", 30, new Vector2(0, 280), new Vector2(840, 280));
            _overlayShare = BrothersUiUtil.MakeText(_overlay.transform, "Share", "", 24, new Vector2(0, 80), new Vector2(840, 80));
            _overlayShare.color = new Color(0.75f, 0.85f, 1f);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                _rogueBtns[i] = BrothersUiUtil.MakeButton(_overlay.transform, "Rogue" + i, "选项", new Vector2(0, -40 - i * 100), new Vector2(780, 88));
                _rogueLabels[i] = _rogueBtns[i].GetComponentInChildren<Text>();
                _rogueBtns[i].onClick.AddListener(() => _session?.PickRogue(idx));
            }

            _btnConfirm = BrothersUiUtil.MakeButton(_overlay.transform, "Confirm", "继续", new Vector2(0, -400), new Vector2(420, 90),
                new Color(0.2f, 0.7f, 0.45f));
            _btnConfirm.onClick.AddListener(OnConfirmOverlay);

            _btnRetry = BrothersUiUtil.MakeButton(_overlay.transform, "Retry", "重玩本局", new Vector2(-200, -400), new Vector2(320, 90),
                new Color(0.85f, 0.45f, 0.25f));
            _btnRetry.onClick.AddListener(() => _session?.RetryAfterSanpang());

            _btnHome = BrothersUiUtil.MakeButton(_overlay.transform, "Home", "回大厅", new Vector2(200, -400), new Vector2(280, 90),
                new Color(0.35f, 0.35f, 0.4f));
            _btnHome.onClick.AddListener(() => _session?.ReturnToHub());

            _btnShare = BrothersUiUtil.MakeButton(_overlay.transform, "Share", "复制分享文案", new Vector2(0, -520), new Vector2(420, 70));
            _btnShare.onClick.AddListener(CopyShare);

            _btnBreakContinue = BrothersUiUtil.MakeButton(_overlay.transform, "BreakGo", "休整结束，继续", new Vector2(0, -400), new Vector2(480, 90),
                new Color(0.2f, 0.7f, 0.45f));
            _btnBreakContinue.onClick.AddListener(() => _session?.FinishBreakAndContinue());

            _btnAdTrain = BrothersUiUtil.MakeButton(_overlay.transform, "BreakAd", "广告+培养点", new Vector2(0, -520), new Vector2(360, 70),
                new Color(0.75f, 0.45f, 0.2f));
            _btnAdTrain.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddTrain(2);
                Refresh();
            }));

            _btnAdReroll = BrothersUiUtil.MakeButton(_overlay.transform, "RogueReroll", "看广告刷新一次",
                new Vector2(-205, -390), new Vector2(380, 72), new Color(0.72f, 0.42f, 0.18f));
            _btnAdReroll.onClick.AddListener(() =>
            {
                if (_session == null || !_session.CanRewardedReroll) return;
                _onRewardedAd?.Invoke(() =>
                {
                    _session.TryRewardedReroll();
                    Refresh();
                });
            });

            _btnAdExpand = BrothersUiUtil.MakeButton(_overlay.transform, "RogueExpand", "看广告扩编 +1",
                new Vector2(205, -390), new Vector2(380, 72), new Color(0.72f, 0.42f, 0.18f));
            _btnAdExpand.onClick.AddListener(() =>
            {
                if (_session == null || !_session.CanRewardedExpand) return;
                _onRewardedAd?.Invoke(() =>
                {
                    _session.TryRewardedExpand();
                    Refresh();
                });
            });
        }

        void OnStartRun()
        {
            if (_session == null) return;
            if (!_session.Meta.IsChapterUnlocked(_selectedChapter))
            {
                _chapterText.text = "未解锁：" + GameTables.FindChapter(_selectedChapter).DisplayName;
                return;
            }

            if (!_session.CanStartRun(_selectedChapter))
            {
                _chapterText.text = "体力不足，可看广告补充";
                return;
            }

            _session.TryStartRun(_selectedChapter);
        }

        void OnConfirmOverlay()
        {
            if (_session == null) return;
            if (_session.Flow == BrothersFlow.Desheng)
                _session.ConfirmDesheng();
        }

        void CopyShare()
        {
            if (_session == null) return;
            string text = BrothersSessionController.ShareText(_session.LastSettle);
            GUIUtility.systemCopyBuffer = text;
            if (_overlayShare != null)
                _overlayShare.text = "已复制：" + text;
        }

        public void Refresh()
        {
            if (_session == null) return;
            if (_btnAdReroll != null) _btnAdReroll.gameObject.SetActive(false);
            if (_btnAdExpand != null) _btnAdExpand.gameObject.SetActive(false);
            switch (_session.Flow)
            {
                case BrothersFlow.Hub:
                case BrothersFlow.Idle:
                    ShowOnlyHub();
                    RefreshHubTexts();
                    break;
                case BrothersFlow.Battling:
                    ShowBattle();
                    RefreshBattle();
                    break;
                case BrothersFlow.Desheng:
                    ShowSettleDesheng();
                    break;
                case BrothersFlow.Sanpang:
                    ShowSettleSanpang();
                    break;
                case BrothersFlow.RoguePick:
                    ShowRogue();
                    break;
                case BrothersFlow.BreakRest:
                    ShowBreak();
                    break;
            }
        }

        void RefreshHubTexts()
        {
            if (_session?.Meta == null) return;
            var m = _session.Meta;
            _metaText.text =
                $"体力 {m.Stamina}/{GameTables.DailyStaminaCap} · 培养点 {m.TrainPoints}\n" +
                $"人情 {m.Favor} · 声望 {m.Renown}\n" +
                $"潜力 生命+{m.PotentialHp} 攻击+{m.PotentialAtk} · 养伤档 {m.HealShortenTier}";

            var ch = GameTables.FindChapter(_selectedChapter);
            string lockText = _session.Meta.IsChapterUnlocked(_selectedChapter) ? "已解锁" : "未解锁";
            _chapterText.text = $"选中：{ch.DisplayName}（{lockText}）\n集合地：{ch.HubPlace}";
        }

        void RefreshBattle()
        {
            if (_session?.Run == null || _battleInfo == null) return;
            var run = _session.Run;
            var scene = GameTables.FindScene(run.CurrentSceneId);
            var battle = _session.Battle;
            var sb = new StringBuilder();
            sb.AppendLine(run.PhaseLabel());
            var timeline = TimelineCatalog.Current(run);
            if (timeline != null)
                sb.AppendLine($"大事件：{timeline.AnchorYear} · {timeline.Title}");
            sb.AppendLine($"场景：{scene?.DisplayName ?? run.CurrentSceneId}");
            string slots = run.HasUnlimitedSlots ? "不限" : run.ActiveSlotLimit.ToString();
            sb.Append($"兄弟 {battle?.AliveBrothers ?? 0}/{slots} · 敌人 {battle?.AliveEnemies ?? 0} · 击杀 {run.KillsThisWave}");
            sb.Append($"\n攻击核心：{run.EquipmentId}");
            sb.Append("\n小队：");
            foreach (var b in run.Squad)
            {
                if (!b.Recruited) continue;
                sb.Append(b.Injured ? $"{b.DisplayName}(伤) " : $"{b.DisplayName} ");
            }

            _battleInfo.text = sb.ToString();
        }

        void ShowOnlyHub()
        {
            _hub.SetActive(true);
            _battleHud.SetActive(false);
            _overlay.SetActive(false);
        }

        void ShowBattle()
        {
            _hub.SetActive(false);
            _battleHud.SetActive(true);
            _overlay.SetActive(false);
        }

        void ShowSettleDesheng()
        {
            _hub.SetActive(false);
            _battleHud.SetActive(false);
            _overlay.SetActive(true);
            SetRogueVisible(false);
            _btnConfirm.gameObject.SetActive(true);
            _btnRetry.gameObject.SetActive(false);
            _btnHome.gameObject.SetActive(false);
            _btnShare.gameObject.SetActive(true);
            _btnBreakContinue.gameObject.SetActive(false);
            _btnAdTrain.gameObject.SetActive(false);
            _overlayTitle.text = "得胜";
            _overlayBody.text = $"{_session.LastSettleDetail}\n下一节点：{_session.NextNodeHint}\n\n{_session.Run?.PhaseLabel()}";
            _overlayShare.text = BrothersSessionController.ShareText(SettleKind.Desheng);
        }

        void ShowSettleSanpang()
        {
            _hub.SetActive(false);
            _battleHud.SetActive(false);
            _overlay.SetActive(true);
            SetRogueVisible(false);
            _btnConfirm.gameObject.SetActive(false);
            _btnRetry.gameObject.SetActive(true);
            _btnHome.gameObject.SetActive(true);
            _btnShare.gameObject.SetActive(true);
            _btnBreakContinue.gameObject.SetActive(false);
            _btnAdTrain.gameObject.SetActive(false);
            _overlayTitle.text = "散旁";
            _overlayBody.text = $"{_session.LastSettleDetail}\n{_session.NextNodeHint}\n培养点安慰奖 +1（已入账）";
            _overlayShare.text = BrothersSessionController.ShareText(SettleKind.Sanpang);
        }

        void ShowRogue()
        {
            _hub.SetActive(false);
            _battleHud.SetActive(false);
            _overlay.SetActive(true);
            SetRogueVisible(true);
            _btnConfirm.gameObject.SetActive(false);
            _btnRetry.gameObject.SetActive(false);
            _btnHome.gameObject.SetActive(false);
            _btnShare.gameObject.SetActive(false);
            _btnBreakContinue.gameObject.SetActive(false);
            _btnAdTrain.gameObject.SetActive(false);
            _overlayTitle.text = "波间选择";
            if (_session.Run != null)
            {
                var run = _session.Run;
                string slots = run.HasUnlimitedSlots ? "不限人数" : $"{run.RecruitedCount}/{run.ActiveSlotLimit}人";
                var timeline = TimelineCatalog.Current(run);
                string eventLine = timeline == null
                    ? ""
                    : $"\n{timeline.AnchorYear} · {timeline.Title}\n机遇：{timeline.OpportunityDesc}\n困难：{timeline.DifficultyDesc}";
                _overlayBody.text =
                    $"{run.PhaseLabel()} · {slots}\n攻击核心：{run.EquipmentId}{eventLine}\n三选一；技能/装备满槽会替换";
            }
            else
            {
                _overlayBody.text = "";
            }
            _overlayShare.text = "";
            _btnAdReroll.gameObject.SetActive(_session.CanRewardedReroll);
            _btnAdExpand.gameObject.SetActive(_session.CanRewardedExpand);

            var opts = _session.CurrentRogue;
            for (int i = 0; i < 3; i++)
            {
                if (i < opts.Count)
                {
                    _rogueBtns[i].gameObject.SetActive(true);
                    _rogueLabels[i].text = $"{opts[i].Title}\n{opts[i].Desc}";
                    _rogueLabels[i].fontSize = 24;
                }
                else
                {
                    _rogueBtns[i].gameObject.SetActive(false);
                }
            }
        }

        void ShowBreak()
        {
            _hub.SetActive(false);
            _battleHud.SetActive(false);
            _overlay.SetActive(true);
            SetRogueVisible(false);
            _btnConfirm.gameObject.SetActive(false);
            _btnRetry.gameObject.SetActive(false);
            _btnHome.gameObject.SetActive(true);
            _btnShare.gameObject.SetActive(false);
            _btnBreakContinue.gameObject.SetActive(true);
            _btnAdTrain.gameObject.SetActive(true);
            _overlayTitle.text = "假期休整";
            _overlayBody.text = $"{_session.LastSettleDetail}\n{_session.NextNodeHint}\n\n{_session.Run?.PhaseLabel()}";
            _overlayShare.text = "";
        }

        void SetRogueVisible(bool on)
        {
            for (int i = 0; i < 3; i++)
                _rogueBtns[i].gameObject.SetActive(on);
        }
    }
}
