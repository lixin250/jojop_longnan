using System;
using System.Collections.Generic;
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
        GameObject _archivePanel;
        GameObject _trainPanel;
        Text _metaText;
        Text _chapterText;
        Text _battleInfo;
        Text _overlayTitle;
        Text _overlayBody;
        Text _overlayShare;
        Text _archiveHint;
        readonly Button[] _rogueBtns = new Button[3];
        readonly Text[] _rogueLabels = new Text[3];
        readonly Image[] _partyPortraits = new Image[5];
        readonly Image[] _partyHpFills = new Image[5];
        readonly Text[] _partyNames = new Text[5];
        readonly float _partyHpWidth = 160f;
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
        readonly List<HeroPickSlot> _heroSlots = new List<HeroPickSlot>();
        ChapterId _selectedChapter = ChapterId.Primary;

        sealed class HeroPickSlot
        {
            public string Id;
            public Image Portrait;
            public Image Frame;
            public Text Name;
            public Text Lock;
        }

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

            BrothersUiUtil.MakePanel(_hub.transform, "HubBg", new Vector2(1080, 1920), Vector2.zero,
                new Color(0.12f, 0.11f, 0.09f, 0.55f));

            var title = BrothersUiUtil.MakeText(_hub.transform, "Title", "我和我的龙兄南弟", 56, new Vector2(0, 820), new Vector2(960, 80));
            title.color = BrothersUiUtil.Parchment;

            _trainPanel = BrothersUiUtil.MakePanel(_hub.transform, "TrainCard", new Vector2(980, 160), new Vector2(0, 700),
                BrothersUiUtil.PanelDark).gameObject;
            _metaText = BrothersUiUtil.MakeText(_trainPanel.transform, "Meta", "", 24, Vector2.zero, new Vector2(920, 140));
            _metaText.alignment = TextAnchor.MiddleLeft;
            _metaText.color = BrothersUiUtil.Parchment;

            _chapterText = BrothersUiUtil.MakeText(_hub.transform, "Chapter", "章节：小学", 26, new Vector2(0, 560), new Vector2(900, 44));
            _chapterText.color = new Color(0.9f, 0.85f, 0.7f);

            var archivePanel = BrothersUiUtil.MakePanel(_hub.transform, "HeroPick", new Vector2(980, 270), new Vector2(0, 360),
                new Color(0.14f, 0.12f, 0.1f, 0.9f));
            _archivePanel = archivePanel.gameObject;
            var archTitle = BrothersUiUtil.MakeText(_archivePanel.transform, "ArchTitle", "初始英雄", 26, new Vector2(0, 108), new Vector2(400, 36));
            archTitle.color = BrothersUiUtil.Parchment;
            _archiveHint = BrothersUiUtil.MakeText(_archivePanel.transform, "ArchHint", "", 20, new Vector2(0, -112), new Vector2(940, 36));
            _archiveHint.color = new Color(0.75f, 0.7f, 0.6f);
            BuildHeroPicker();

            float y = 120f;
            foreach (var ch in GameTables.Chapters)
            {
                var id = ch.Id;
                var btn = BrothersUiUtil.MakeButton(_hub.transform, "Ch_" + id, ch.DisplayName, new Vector2(0, y), new Vector2(640, 68),
                    new Color(0.32f, 0.28f, 0.22f));
                btn.onClick.AddListener(() =>
                {
                    _selectedChapter = id;
                    RefreshHubTexts();
                });
                y -= 76f;
            }

            var start = BrothersUiUtil.MakeButton(_hub.transform, "BtnStart", "出征（耗体力）", new Vector2(0, -380), new Vector2(520, 96),
                BrothersUiUtil.AccentGreen);
            start.onClick.AddListener(OnStartRun);

            var potHp = BrothersUiUtil.MakeButton(_hub.transform, "BtnPotHp", "潜力生命 3点", new Vector2(-220, -500), new Vector2(280, 66),
                new Color(0.28f, 0.45f, 0.55f));
            potHp.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyPotentialHp();
                RefreshHubTexts();
            });
            var potAtk = BrothersUiUtil.MakeButton(_hub.transform, "BtnPotAtk", "潜力攻击 3点", new Vector2(220, -500), new Vector2(280, 66),
                new Color(0.55f, 0.32f, 0.28f));
            potAtk.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyPotentialAtk();
                RefreshHubTexts();
            });
            var heal = BrothersUiUtil.MakeButton(_hub.transform, "BtnHeal", "养伤缩短 5点", new Vector2(0, -580), new Vector2(360, 66),
                new Color(0.35f, 0.5f, 0.35f));
            heal.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyHealTier();
                RefreshHubTexts();
            });

            var adSta = BrothersUiUtil.MakeButton(_hub.transform, "BtnAdSta", "广告+体力", new Vector2(-220, -680), new Vector2(280, 66),
                BrothersUiUtil.AccentOrange);
            adSta.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddStamina(2);
                RefreshHubTexts();
            }));
            var adTrain = BrothersUiUtil.MakeButton(_hub.transform, "BtnAdTrain", "广告+培养点", new Vector2(220, -680), new Vector2(280, 66),
                BrothersUiUtil.AccentOrange);
            adTrain.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddTrain(3);
                RefreshHubTexts();
            }));

            var home = BrothersUiUtil.MakeButton(_hub.transform, "BtnBack", "返回主界面", new Vector2(0, -780), new Vector2(360, 66),
                new Color(0.35f, 0.35f, 0.4f));
            home.onClick.AddListener(() => _onHome?.Invoke());
        }

        void BuildHeroPicker()
        {
            var candidates = HeroUnlock.StarterCandidates();
            const int cols = 7;
            const float cell = 118f;
            float startX = -((cols - 1) * cell) * 0.5f;
            float startY = 32f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var role = candidates[i];
                int col = i % cols;
                int row = i / cols;
                var pos = new Vector2(startX + col * cell, startY - row * 102f);
                var frame = BrothersUiUtil.MakePanel(_archivePanel.transform, "HeroF" + i,
                    new Vector2(78, 78), pos, new Color(0.28f, 0.26f, 0.22f, 0.95f));
                var sp = RoleArtLoader.LoadPortrait(role.AvatarLoc);
                var img = BrothersUiUtil.MakePortrait(_archivePanel.transform, "Hero" + i,
                    pos, new Vector2(70, 70), sp, new Color(0.35f, 0.32f, 0.28f));
                var btn = img.gameObject.AddComponent<Button>();
                string id = role.Id;
                btn.onClick.AddListener(() => OnPickHero(id));
                var name = BrothersUiUtil.MakeText(_archivePanel.transform, "HeroN" + i, role.Name, 16,
                    pos + new Vector2(0f, -48f), new Vector2(110, 24));
                name.color = BrothersUiUtil.Parchment;
                var lockTxt = BrothersUiUtil.MakeText(_archivePanel.transform, "HeroL" + i, "", 14,
                    pos + new Vector2(0f, 0f), new Vector2(70, 70));
                lockTxt.color = new Color(1f, 0.92f, 0.55f, 0.95f);
                _heroSlots.Add(new HeroPickSlot
                {
                    Id = role.Id,
                    Portrait = img,
                    Frame = frame,
                    Name = name,
                    Lock = lockTxt
                });
            }
        }

        void OnPickHero(string roleId)
        {
            if (_session?.Meta == null) return;
            var role = RoleCatalog.FindRole(roleId);
            if (role == null) return;
            if (_session.Meta.TrySelectHero(roleId))
            {
                RefreshHeroPicker();
                return;
            }

            if (_archiveHint != null)
                _archiveHint.text = HeroUnlock.Hint(role, _session.Meta);
        }

        void RefreshHeroPicker()
        {
            if (_session?.Meta == null) return;
            if (_heroSlots.Count == 0)
                BuildHeroPicker();
            var meta = _session.Meta;
            string selected = meta.SelectedHeroId;
            RoleList selectedRole = null;
            foreach (var slot in _heroSlots)
            {
                var role = RoleCatalog.FindRole(slot.Id);
                if (role == null) continue;
                bool unlocked = HeroUnlock.IsUnlocked(role, meta);
                bool isSelected = slot.Id == selected;
                if (isSelected) selectedRole = role;
                slot.Portrait.color = unlocked ? Color.white : new Color(0.28f, 0.28f, 0.3f, 0.85f);
                slot.Frame.color = isSelected
                    ? new Color(0.92f, 0.74f, 0.28f, 1f)
                    : unlocked
                        ? new Color(0.32f, 0.42f, 0.34f, 0.95f)
                        : new Color(0.22f, 0.2f, 0.2f, 0.9f);
                slot.Lock.text = unlocked ? "" : "锁";
                slot.Name.color = isSelected ? new Color(1f, 0.86f, 0.4f) : BrothersUiUtil.Parchment;
            }

            if (_archiveHint == null) return;
            if (selectedRole != null)
                _archiveHint.text = $"{selectedRole.Name} · {HeroUnlock.Hint(selectedRole, meta)}";
            else
                _archiveHint.text = "点头像选择出征初始英雄；未解锁的会显示条件";
        }

        void BuildBattleHud()
        {
            _battleHud = new GameObject("BattleHud", typeof(RectTransform));
            _battleHud.transform.SetParent(transform, false);
            BrothersUiUtil.Stretch(_battleHud.GetComponent<RectTransform>());

            BrothersUiUtil.MakePanel(_battleHud.transform, "TopBar", new Vector2(1040, 220), new Vector2(0, 820),
                new Color(0.06f, 0.07f, 0.09f, 0.82f));
            _battleInfo = BrothersUiUtil.MakeText(_battleHud.transform, "Info", "", 24, new Vector2(0, 860), new Vector2(980, 120));
            _battleInfo.alignment = TextAnchor.UpperLeft;
            _battleInfo.color = BrothersUiUtil.Parchment;

            var tip = BrothersUiUtil.MakeText(_battleHud.transform, "Tip", "自动割草中 · 头顶血条 / 飘字已开", 22,
                new Vector2(0, 720), new Vector2(800, 36));
            tip.color = new Color(0.85f, 0.8f, 0.65f);

            // 底部小队卡片
            BrothersUiUtil.MakePanel(_battleHud.transform, "PartyBar", new Vector2(1040, 200), new Vector2(0, -780),
                new Color(0.06f, 0.07f, 0.09f, 0.88f));
            float px = -380f;
            for (int i = 0; i < _partyPortraits.Length; i++)
            {
                _partyPortraits[i] = BrothersUiUtil.MakePortrait(_battleHud.transform, "PPort" + i,
                    new Vector2(px + i * 190f, -740), new Vector2(88, 88), null, new Color(0.25f, 0.28f, 0.32f));
                _partyHpFills[i] = BrothersUiUtil.MakeHpFill(_battleHud.transform, "PHp" + i,
                    new Vector2(px + i * 190f, -820), new Vector2(_partyHpWidth, 18), BrothersUiUtil.BrotherHp);
                _partyNames[i] = BrothersUiUtil.MakeText(_battleHud.transform, "PName" + i, "", 18,
                    new Vector2(px + i * 190f, -860), new Vector2(170, 28));
                _partyNames[i].color = BrothersUiUtil.Parchment;
                _partyPortraits[i].gameObject.SetActive(false);
                _partyHpFills[i].gameObject.SetActive(false);
                _partyNames[i].gameObject.SetActive(false);
            }

            var quit = BrothersUiUtil.MakeButton(_battleHud.transform, "BtnQuit", "弃战回大厅", new Vector2(400, -700), new Vector2(240, 56),
                new Color(0.45f, 0.22f, 0.22f));
            quit.onClick.AddListener(() => _session?.ReturnToHub());
        }

        void BuildOverlay()
        {
            var panel = BrothersUiUtil.MakePanel(transform, "Overlay", new Vector2(920, 1200), Vector2.zero,
                new Color(0.10f, 0.09f, 0.08f, 0.96f));
            _overlay = panel.gameObject;

            _overlayTitle = BrothersUiUtil.MakeText(_overlay.transform, "Title", "", 52, new Vector2(0, 480), new Vector2(860, 80));
            _overlayTitle.color = BrothersUiUtil.Parchment;
            _overlayBody = BrothersUiUtil.MakeText(_overlay.transform, "Body", "", 28, new Vector2(0, 280), new Vector2(840, 280));
            _overlayBody.color = new Color(0.92f, 0.88f, 0.78f);
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
                $"【培养】体力 {m.Stamina}/{GameTables.DailyStaminaCap}    培养点 {m.TrainPoints}\n" +
                $"人情 {m.Favor} · 声望 {m.Renown}\n" +
                $"潜力 生命+{m.PotentialHp} 攻击+{m.PotentialAtk} · 养伤档 {m.HealShortenTier}";

            var ch = GameTables.FindChapter(_selectedChapter);
            string lockText = _session.Meta.IsChapterUnlocked(_selectedChapter) ? "已解锁" : "未解锁";
            _chapterText.text = $"选中：{ch.DisplayName}（{lockText}）· 集合地：{ch.HubPlace}";

            if (_archiveHint != null)
                RefreshHeroPicker();
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
            _battleInfo.text = sb.ToString();

            RefreshPartyCards(run);
        }

        void RefreshPartyCards(RunState run)
        {
            int slot = 0;
            foreach (var b in run.Squad)
            {
                if (!b.Recruited || slot >= _partyPortraits.Length) continue;
                _partyPortraits[slot].gameObject.SetActive(true);
                _partyHpFills[slot].gameObject.SetActive(true);
                _partyNames[slot].gameObject.SetActive(true);

                var sp = JojoP.Gameplay.Brothers.RoleArtLoader.LoadPortrait(b.AvatarLoc);
                if (sp != null)
                {
                    _partyPortraits[slot].sprite = sp;
                    _partyPortraits[slot].color = Color.white;
                    _partyPortraits[slot].preserveAspect = true;
                }
                else
                {
                    _partyPortraits[slot].sprite = null;
                    _partyPortraits[slot].color = b.Injured
                        ? new Color(0.35f, 0.2f, 0.2f)
                        : new Color(0.3f, 0.45f, 0.55f);
                }

                float ratio = b.MaxHp > 0.01f ? b.Hp / b.MaxHp : 0f;
                if (b.Injured) ratio = 0f;
                BrothersUiUtil.SetHpFill(_partyHpFills[slot], ratio, _partyHpWidth);
                _partyHpFills[slot].color = ratio < 0.35f
                    ? BrothersUiUtil.AccentOrange
                    : BrothersUiUtil.BrotherHp;
                _partyNames[slot].text = b.Injured ? $"{b.DisplayName}·伤" : b.DisplayName;
                slot++;
            }

            for (int i = slot; i < _partyPortraits.Length; i++)
            {
                _partyPortraits[i].gameObject.SetActive(false);
                _partyHpFills[i].gameObject.SetActive(false);
                _partyNames[i].gameObject.SetActive(false);
            }
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
