using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using JojoP.Cfg;
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
        GameObject _pageHero;
        GameObject _pageStage;
        GameObject _detail;
        Text _detailName;
        Text _detailHint;
        Image _detailPortrait;
        Button _detailSelect;
        string _detailRoleId;
        Image _overlayPortrait;
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

            var title = BrothersUiUtil.MakeText(_hub.transform, "Title", "JojoP", 64, new Vector2(0, 840), new Vector2(960, 88));
            title.color = BrothersUiUtil.Parchment;
            var logoHint = BrothersUiUtil.MakeText(_hub.transform, "LogoHint", "大图标后续替换", 20, new Vector2(0, 780), new Vector2(600, 32));
            logoHint.color = new Color(0.7f, 0.65f, 0.55f, 0.8f);

            _pageHero = new GameObject("PageHero", typeof(RectTransform));
            _pageHero.transform.SetParent(_hub.transform, false);
            FitPage(_pageHero.GetComponent<RectTransform>());

            _pageStage = new GameObject("PageStage", typeof(RectTransform));
            _pageStage.transform.SetParent(_hub.transform, false);
            FitPage(_pageStage.GetComponent<RectTransform>());
            _pageStage.SetActive(false);

            var archivePanel = BrothersUiUtil.MakePanel(_pageHero.transform, "HeroPick", new Vector2(980, 980), new Vector2(0, 40),
                new Color(0.14f, 0.12f, 0.1f, 0.9f));
            _archivePanel = archivePanel.gameObject;
            var archTitle = BrothersUiUtil.MakeText(_archivePanel.transform, "ArchTitle", "选择英雄", 28, new Vector2(0, 440), new Vector2(400, 40));
            archTitle.color = BrothersUiUtil.Parchment;
            _archiveHint = BrothersUiUtil.MakeText(_archivePanel.transform, "ArchHint", "点头像看详情；未解锁也会显示条件", 20,
                new Vector2(0, -450), new Vector2(940, 40));
            _archiveHint.color = new Color(0.75f, 0.7f, 0.6f);
            BuildHeroPicker();

            _trainPanel = BrothersUiUtil.MakePanel(_pageStage.transform, "TrainCard", new Vector2(980, 160), new Vector2(0, 620),
                BrothersUiUtil.PanelDark).gameObject;
            _metaText = BrothersUiUtil.MakeText(_trainPanel.transform, "Meta", "", 24, Vector2.zero, new Vector2(920, 140));
            _metaText.alignment = TextAnchor.MiddleLeft;
            _metaText.color = BrothersUiUtil.Parchment;

            _chapterText = BrothersUiUtil.MakeText(_pageStage.transform, "Node", "小学 · 小1 · 上学期", 30, new Vector2(0, 430), new Vector2(900, 90));
            _chapterText.color = new Color(0.9f, 0.85f, 0.7f);
            var stageLock = BrothersUiUtil.MakeText(_pageStage.transform, "StageLock",
                "关卡随波次自动推进，当前不可手选", 22, new Vector2(0, 360), new Vector2(880, 40));
            stageLock.color = new Color(0.72f, 0.68f, 0.58f);

            var start = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnStart", "开始", new Vector2(0, 220), new Vector2(520, 110),
                BrothersUiUtil.AccentGreen);
            start.onClick.AddListener(OnStartRun);

            var potHp = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnPotHp", "潜力生命 3点", new Vector2(-220, 80), new Vector2(280, 66),
                new Color(0.28f, 0.45f, 0.55f));
            potHp.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyPotentialHp();
                RefreshHubTexts();
            });
            var potAtk = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnPotAtk", "潜力攻击 3点", new Vector2(220, 80), new Vector2(280, 66),
                new Color(0.55f, 0.32f, 0.28f));
            potAtk.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyPotentialAtk();
                RefreshHubTexts();
            });
            var heal = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnHeal", "养伤缩短 5点", new Vector2(0, -10), new Vector2(360, 66),
                new Color(0.35f, 0.5f, 0.35f));
            heal.onClick.AddListener(() =>
            {
                _session?.Meta.TryBuyHealTier();
                RefreshHubTexts();
            });

            var adSta = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnAdSta", "广告+体力", new Vector2(-220, -110), new Vector2(280, 66),
                BrothersUiUtil.AccentOrange);
            adSta.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddStamina(2);
                RefreshHubTexts();
            }));
            var adTrain = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnAdTrain", "广告+培养点", new Vector2(220, -110), new Vector2(280, 66),
                BrothersUiUtil.AccentOrange);
            adTrain.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddTrain(3);
                RefreshHubTexts();
            }));

            var backHero = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnBackHero", "重选英雄", new Vector2(0, -220), new Vector2(360, 66),
                new Color(0.32f, 0.3f, 0.28f));
            backHero.onClick.AddListener(() => ShowHubPage(stage: false, animate: true));

            var home = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnBack", "返回主界面", new Vector2(0, -320), new Vector2(360, 66),
                new Color(0.35f, 0.35f, 0.4f));
            home.onClick.AddListener(() => _onHome?.Invoke());

            var homeHero = BrothersUiUtil.MakeButton(_pageHero.transform, "BtnBackMenu", "返回主界面", new Vector2(0, -820), new Vector2(360, 66),
                new Color(0.35f, 0.35f, 0.4f));
            homeHero.onClick.AddListener(() => _onHome?.Invoke());

            BuildHeroDetail();
        }

        void BuildHeroDetail()
        {
            var panel = BrothersUiUtil.MakePanel(_hub.transform, "HeroDetail", new Vector2(860, 1080), Vector2.zero,
                new Color(0.09f, 0.08f, 0.07f, 0.97f));
            _detail = panel.gameObject;
            _detail.SetActive(false);

            _detailPortrait = BrothersUiUtil.MakePortrait(_detail.transform, "Port", new Vector2(0, 280), new Vector2(280, 360),
                null, new Color(0.2f, 0.18f, 0.16f));
            _detailName = BrothersUiUtil.MakeText(_detail.transform, "Name", "", 40, new Vector2(0, 40), new Vector2(760, 56));
            _detailName.color = BrothersUiUtil.Parchment;
            _detailHint = BrothersUiUtil.MakeText(_detail.transform, "Hint", "", 26, new Vector2(0, -120), new Vector2(760, 220));
            _detailHint.color = new Color(0.9f, 0.85f, 0.72f);

            _detailSelect = BrothersUiUtil.MakeButton(_detail.transform, "Select", "以此出征", new Vector2(0, -360), new Vector2(420, 90),
                BrothersUiUtil.AccentGreen);
            _detailSelect.onClick.AddListener(OnConfirmHero);

            var close = BrothersUiUtil.MakeButton(_detail.transform, "Close", "关闭", new Vector2(0, -470), new Vector2(280, 70),
                new Color(0.35f, 0.35f, 0.4f));
            close.onClick.AddListener(() => _detail.SetActive(false));
        }

        void BuildHeroPicker()
        {
            var candidates = HeroUnlock.StarterCandidates();
            const int cols = 5;
            const float cell = 168f;
            float startX = -((cols - 1) * cell) * 0.5f;
            float startY = 280f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var role = candidates[i];
                int col = i % cols;
                int row = i / cols;
                var pos = new Vector2(startX + col * cell, startY - row * 150f);
                var frame = BrothersUiUtil.MakePanel(_archivePanel.transform, "HeroF" + i,
                    new Vector2(110, 110), pos, new Color(0.28f, 0.26f, 0.22f, 0.95f));
                var sp = RoleArtLoader.LoadPortrait(role.AvatarLoc);
                var img = BrothersUiUtil.MakePortrait(_archivePanel.transform, "Hero" + i,
                    pos, new Vector2(98, 98), sp, new Color(0.35f, 0.32f, 0.28f));
                var btn = img.gameObject.AddComponent<Button>();
                string id = role.Id;
                btn.onClick.AddListener(() => OnPickHero(id));
                var name = BrothersUiUtil.MakeText(_archivePanel.transform, "HeroN" + i, role.Name, 20,
                    pos + new Vector2(0f, -72f), new Vector2(150, 28));
                name.color = BrothersUiUtil.Parchment;
                var lockTxt = BrothersUiUtil.MakeText(_archivePanel.transform, "HeroL" + i, "", 18,
                    pos + new Vector2(0f, 0f), new Vector2(98, 98));
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
            OpenHeroDetail(roleId);
        }

        void OpenHeroDetail(string roleId)
        {
            if (_session?.Meta == null || _detail == null) return;
            var role = RoleCatalog.FindRole(roleId);
            if (role == null) return;
            _detailRoleId = roleId;
            _detail.SetActive(true);
            _detailName.text = role.Name;
            _detailHint.text = HeroUnlock.Hint(role, _session.Meta);
            var sp = RoleArtLoader.LoadPoster(role.AvatarLoc) ?? RoleArtLoader.LoadPortrait(role.AvatarLoc);
            _detailPortrait.sprite = sp;
            _detailPortrait.color = sp != null ? Color.white : new Color(0.3f, 0.28f, 0.24f);
            _detailPortrait.preserveAspect = true;
            bool can = HeroUnlock.CanSelect(roleId, _session.Meta);
            _detailSelect.gameObject.SetActive(can);
            _detailSelect.GetComponentInChildren<Text>().text = can ? "以此出征" : "未解锁";
        }

        void OnConfirmHero()
        {
            if (_session?.Meta == null || string.IsNullOrEmpty(_detailRoleId)) return;
            if (!_session.Meta.TrySelectHero(_detailRoleId)) return;
            _detail.SetActive(false);
            RefreshHeroPicker();
            ShowHubPage(stage: true, animate: true);
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
                _archiveHint.text = $"当前：{selectedRole.Name} · 点头像看详情";
            else
                _archiveHint.text = "点头像看详情；未解锁也会显示条件";
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

            var tip = BrothersUiUtil.MakeText(_battleHud.transform, "Tip", "坚持到计时结束 · 头顶血条 / 受击顿帧", 22,
                new Vector2(0, 720), new Vector2(800, 36));
            tip.color = new Color(0.85f, 0.8f, 0.65f);

            var zoomIn = BrothersUiUtil.MakeButton(_battleHud.transform, "ZoomIn", "拉近", new Vector2(380, 760), new Vector2(140, 56),
                new Color(0.22f, 0.32f, 0.4f));
            zoomIn.onClick.AddListener(() => BattleCamera.Ensure(Camera.main)?.ZoomBy(-0.7f));
            var zoomOut = BrothersUiUtil.MakeButton(_battleHud.transform, "ZoomOut", "拉远", new Vector2(530, 760), new Vector2(140, 56),
                new Color(0.22f, 0.32f, 0.4f));
            zoomOut.onClick.AddListener(() => BattleCamera.Ensure(Camera.main)?.ZoomBy(0.7f));

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
            _overlayPortrait = BrothersUiUtil.MakePortrait(_overlay.transform, "WinPort", new Vector2(0, 300), new Vector2(220, 280),
                null, new Color(0.16f, 0.14f, 0.12f));
            _overlayBody = BrothersUiUtil.MakeText(_overlay.transform, "Body", "", 28, new Vector2(0, 40), new Vector2(840, 200));
            _overlayBody.color = new Color(0.92f, 0.88f, 0.78f);
            _overlayShare = BrothersUiUtil.MakeText(_overlay.transform, "Share", "", 24, new Vector2(0, -150), new Vector2(840, 80));
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
            _selectedChapter = ChapterId.Primary;
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

            var ch = GameTables.FindChapter(ChapterId.Primary);
            var hero = RoleCatalog.FindRole(_session.Meta.SelectedHeroId);
            _chapterText.text = $"{ch.DisplayName} · 小1 · 上学期\n出征：{hero?.Name ?? "猩哥"}";

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
            if (battle != null && battle.WaveDuration > 0.01f)
            {
                int sec = Mathf.CeilToInt(battle.WaveLeft);
                sb.Append($"\n波次剩余 {sec:00} 秒  ·  坚持到计时结束");
            }
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
            if (_detail != null) _detail.SetActive(false);
            ShowHubPage(stage: false, animate: false);
        }

        void ShowHubPage(bool stage, bool animate)
        {
            StopAllCoroutines();
            if (!animate || _pageHero == null || _pageStage == null)
            {
                _pageHero.SetActive(!stage);
                _pageStage.SetActive(stage);
                return;
            }

            StartCoroutine(SlideHub(stage));
        }

        static void FitPage(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1080f, 1920f);
            rt.anchoredPosition = Vector2.zero;
        }

        IEnumerator SlideHub(bool toStage)
        {
            _pageHero.SetActive(true);
            _pageStage.SetActive(true);
            var from = toStage ? _pageHero.GetComponent<RectTransform>() : _pageStage.GetComponent<RectTransform>();
            var to = toStage ? _pageStage.GetComponent<RectTransform>() : _pageHero.GetComponent<RectTransform>();
            from.anchoredPosition = Vector2.zero;
            to.anchoredPosition = new Vector2(toStage ? 1080f : -1080f, 0f);
            const float dur = 0.28f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                from.anchoredPosition = new Vector2((toStage ? -1080f : 1080f) * k, 0f);
                to.anchoredPosition = new Vector2((toStage ? 1080f : -1080f) * (1f - k), 0f);
                yield return null;
            }

            from.anchoredPosition = Vector2.zero;
            to.anchoredPosition = Vector2.zero;
            _pageHero.SetActive(!toStage);
            _pageStage.SetActive(toStage);
        }

        void Update()
        {
            if (_session != null && _session.Flow == BrothersFlow.Battling && _battleHud != null && _battleHud.activeSelf)
                RefreshBattle();
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
            var lead = _session.Run?.Squad != null && _session.Run.Squad.Count > 0 ? _session.Run.Squad[0] : null;
            var winSp = lead != null ? RoleArtLoader.LoadPoster(lead.AvatarLoc) ?? RoleArtLoader.LoadHalf(lead.AvatarLoc) : null;
            if (_overlayPortrait != null)
            {
                _overlayPortrait.gameObject.SetActive(winSp != null);
                _overlayPortrait.sprite = winSp;
                _overlayPortrait.color = Color.white;
                _overlayPortrait.preserveAspect = true;
            }
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
            if (_overlayPortrait != null) _overlayPortrait.gameObject.SetActive(false);
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
            if (_overlayPortrait != null) _overlayPortrait.gameObject.SetActive(false);
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
            if (_overlayPortrait != null) _overlayPortrait.gameObject.SetActive(false);
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
