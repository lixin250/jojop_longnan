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
        Text _trainHint;
        StatRadarGraphic _radar;
        RectTransform _radarRoot;
        Image _radarDimmer;
        GameObject _radarLayer;
        bool _radarExpanded;
        Coroutine _slideCo;
        Coroutine _radarCo;
        Text _statLine;
        readonly float[] _radarInner = new float[HeroStatPreview.Axes];
        readonly float[] _radarOuter = new float[HeroStatPreview.Axes];
        Button _btnPotHp;
        Button _btnPotAtk;
        Button _btnPotDef;
        Button _btnHeal;
        Button _heroPotHp;
        Button _heroPotAtk;
        Button _heroPotDef;
        Button _btnHubBack;
        Button _btnBattleBack;
        Button _btnOverlayBack;
        Text _chapterText;
        Text _battleInfo;
        Text _overlayTitle;
        Text _overlayBody;
        Text _overlayShare;
        Text _archiveHint;
        GameObject _pageHero;
        GameObject _pageStage;
        Text _detailName;
        Text _detailHint;
        Button _detailSelect;
        string _detailRoleId;
        Image _poseImage;
        HeroPosePreview _posePreview;
        Image _overlayPortrait;
        sealed class RogueCardUi
        {
            public GameObject Root;
            public Button Button;
            public Image Frame;
            public Image Art;
            public Text Title;
            public Text Kind;
            public Text Desc;
        }

        readonly RogueCardUi[] _rogueCards = new RogueCardUi[3];
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

            var title = BrothersUiUtil.MakeText(_hub.transform, "Title", "JojoP", 48, new Vector2(0, 900), new Vector2(960, 56));
            title.color = BrothersUiUtil.Parchment;

            _pageHero = new GameObject("PageHero", typeof(RectTransform));
            _pageHero.transform.SetParent(_hub.transform, false);
            FitPage(_pageHero.GetComponent<RectTransform>());

            _pageStage = new GameObject("PageStage", typeof(RectTransform));
            _pageStage.transform.SetParent(_hub.transform, false);
            FitPage(_pageStage.GetComponent<RectTransform>());
            _pageStage.SetActive(false);

            var strip = BrothersUiUtil.MakeHScroll(_pageHero.transform, "HeroStrip", new Vector2(0, 660), new Vector2(1040, 160));
            _archivePanel = strip.gameObject;
            _archiveHint = BrothersUiUtil.MakeText(_pageHero.transform, "StripHint", "左右滑选人 · 蓝=初始 绿=培养", 20,
                new Vector2(0, 555), new Vector2(900, 32));
            _archiveHint.color = new Color(0.72f, 0.68f, 0.58f);

            var posePanel = BrothersUiUtil.MakePanel(_pageHero.transform, "PoseStage", new Vector2(460, 560),
                new Vector2(-250, 50), new Color(0.08f, 0.07f, 0.06f, 0.94f));
            _poseImage = BrothersUiUtil.MakePortrait(posePanel.transform, "Pose", new Vector2(0, 16), new Vector2(420, 500),
                null, new Color(0.18f, 0.16f, 0.14f));
            _posePreview = posePanel.gameObject.AddComponent<HeroPosePreview>();
            _posePreview.Bind(_poseImage);
            var poseTip = BrothersUiUtil.MakeText(posePanel.transform, "PoseTip", "idle / atk", 18, new Vector2(0, -252), new Vector2(400, 28));
            poseTip.color = new Color(0.65f, 0.6f, 0.5f);

            var infoPanel = BrothersUiUtil.MakePanel(_pageHero.transform, "HeroInfo", new Vector2(500, 560),
                new Vector2(270, 50), new Color(0.10f, 0.09f, 0.08f, 0.94f));
            infoPanel.gameObject.AddComponent<RectMask2D>();
            _detailName = BrothersUiUtil.MakeText(infoPanel.transform, "Name", "", 32, new Vector2(0, 248), new Vector2(460, 40));
            _detailName.alignment = TextAnchor.MiddleCenter;
            _detailName.color = BrothersUiUtil.Parchment;

            _detailHint = BrothersUiUtil.MakeText(infoPanel.transform, "Body", "", 20, new Vector2(0, -20), new Vector2(460, 460));
            _detailHint.alignment = TextAnchor.UpperLeft;
            _detailHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailHint.verticalOverflow = VerticalWrapMode.Truncate;
            _detailHint.color = new Color(0.9f, 0.85f, 0.72f);

            _detailSelect = BrothersUiUtil.MakeButton(_pageHero.transform, "Select", "以此出征", new Vector2(0, -500), new Vector2(480, 88),
                BrothersUiUtil.AccentGreen);
            _detailSelect.onClick.AddListener(OnConfirmHero);

            BuildHeroPicker();

            var chrome = BrothersUiUtil.MakePanel(_hub.transform, "HubChrome", new Vector2(1040, 88), new Vector2(0, 818),
                new Color(0.08f, 0.08f, 0.1f, 0.92f));
            _btnHubBack = BrothersUiUtil.MakeButton(chrome.transform, "Back", "返回", new Vector2(-430, 0), new Vector2(140, 64),
                new Color(0.32f, 0.32f, 0.36f));
            _btnHubBack.onClick.AddListener(OnHubBack);
            _metaText = BrothersUiUtil.MakeText(chrome.transform, "Meta", "", 22, new Vector2(80, 0), new Vector2(820, 80));
            _metaText.alignment = TextAnchor.MiddleLeft;
            _metaText.color = BrothersUiUtil.Parchment;
            chrome.transform.SetAsLastSibling();
            BuildRadarOverlay();

            _trainPanel = BrothersUiUtil.MakePanel(_pageStage.transform, "TrainCard", new Vector2(980, 110), new Vector2(0, 620),
                BrothersUiUtil.PanelDark).gameObject;
            _trainHint = BrothersUiUtil.MakeText(_trainPanel.transform, "TrainHint", "", 24, Vector2.zero, new Vector2(920, 90));
            _trainHint.alignment = TextAnchor.MiddleLeft;
            _trainHint.color = BrothersUiUtil.Parchment;

            _chapterText = BrothersUiUtil.MakeText(_pageStage.transform, "Node", "小学 · 小1 · 上学期", 30, new Vector2(0, 480), new Vector2(900, 90));
            _chapterText.color = new Color(0.9f, 0.85f, 0.7f);
            var stageLock = BrothersUiUtil.MakeText(_pageStage.transform, "StageLock",
                "关卡随波次自动推进，当前不可手选", 22, new Vector2(0, 410), new Vector2(880, 40));
            stageLock.color = new Color(0.72f, 0.68f, 0.58f);

            var start = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnStart", "开始", new Vector2(0, 270), new Vector2(520, 110),
                BrothersUiUtil.AccentGreen);
            start.onClick.AddListener(OnStartRun);

            _btnPotHp = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnPotHp", "潜力生命 3点", new Vector2(-280, 130), new Vector2(260, 66),
                new Color(0.28f, 0.45f, 0.55f));
            _btnPotHp.onClick.AddListener(() => SpendPotential(m => m.TryBuyPotentialHp()));
            _btnPotAtk = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnPotAtk", "潜力攻击 3点", new Vector2(0, 130), new Vector2(260, 66),
                new Color(0.55f, 0.32f, 0.28f));
            _btnPotAtk.onClick.AddListener(() => SpendPotential(m => m.TryBuyPotentialAtk()));
            _btnPotDef = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnPotDef", "潜力防御 3点", new Vector2(280, 130), new Vector2(260, 66),
                new Color(0.32f, 0.4f, 0.52f));
            _btnPotDef.onClick.AddListener(() => SpendPotential(m => m.TryBuyPotentialDef()));
            _btnHeal = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnHeal", "养伤缩短 5点", new Vector2(0, 40), new Vector2(360, 66),
                new Color(0.35f, 0.5f, 0.35f));
            _btnHeal.onClick.AddListener(() => SpendPotential(m => m.TryBuyHealTier()));

            var adSta = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnAdSta", "广告+体力", new Vector2(-220, -50), new Vector2(280, 66),
                BrothersUiUtil.AccentOrange);
            adSta.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddStamina(2);
                RefreshHubTexts();
            }));
            var adTrain = BrothersUiUtil.MakeButton(_pageStage.transform, "BtnAdTrain", "广告+培养点", new Vector2(220, -50), new Vector2(280, 66),
                BrothersUiUtil.AccentOrange);
            adTrain.onClick.AddListener(() => _onRewardedAd?.Invoke(() =>
            {
                _session?.Meta.AddTrain(3);
                RefreshHubTexts();
            }));
        }

        void BuildRadarOverlay()
        {
            _radarLayer = new GameObject("RadarLayer", typeof(RectTransform));
            _radarLayer.transform.SetParent(_hub.transform, false);
            BrothersUiUtil.Stretch(_radarLayer.GetComponent<RectTransform>());

            var dimmerGo = new GameObject("RadarDimmer", typeof(RectTransform), typeof(Image), typeof(Button));
            dimmerGo.transform.SetParent(_radarLayer.transform, false);
            BrothersUiUtil.Stretch(dimmerGo.GetComponent<RectTransform>());
            _radarDimmer = dimmerGo.GetComponent<Image>();
            _radarDimmer.color = new Color(0f, 0f, 0f, 0.82f);
            _radarDimmer.raycastTarget = true;
            var dimBtn = dimmerGo.GetComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            dimBtn.onClick.AddListener(() => SetRadarExpanded(false));
            dimmerGo.SetActive(false);

            var radarHost = new GameObject("RadarHost", typeof(RectTransform));
            radarHost.transform.SetParent(_radarLayer.transform, false);
            _radarRoot = radarHost.GetComponent<RectTransform>();
            _radarRoot.anchorMin = _radarRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _radarRoot.pivot = new Vector2(0.5f, 0.5f);
            ApplyRadarCollapsed(instant: true);

            var radarGo = new GameObject("Radar", typeof(RectTransform), typeof(CanvasRenderer), typeof(StatRadarGraphic), typeof(Button));
            radarGo.transform.SetParent(radarHost.transform, false);
            var radarRt = radarGo.GetComponent<RectTransform>();
            radarRt.anchorMin = radarRt.anchorMax = new Vector2(0.5f, 0.5f);
            radarRt.pivot = new Vector2(0.5f, 0.5f);
            BrothersUiUtil.Stretch(radarRt);
            _radar = radarGo.GetComponent<StatRadarGraphic>();
            _radar.color = Color.white;
            _radar.raycastTarget = true;
            _radar.EnsureLabels(HeroStatPreview.AxisNames);
            var radarBtn = radarGo.GetComponent<Button>();
            radarBtn.transition = Selectable.Transition.None;
            radarBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            radarBtn.onClick.AddListener(OnRadarClicked);

            _heroPotHp = BrothersUiUtil.MakePlus(radarHost.transform, "PlusHp", BrothersUiUtil.PlusOrange);
            _heroPotHp.onClick.AddListener(() => SpendPotential(m => m.TryBuyPotentialHp()));
            _heroPotAtk = BrothersUiUtil.MakePlus(radarHost.transform, "PlusAtk", BrothersUiUtil.PlusOrange);
            _heroPotAtk.onClick.AddListener(() => SpendPotential(m => m.TryBuyPotentialAtk()));
            _heroPotDef = BrothersUiUtil.MakePlus(radarHost.transform, "PlusDef", BrothersUiUtil.PlusOrange);
            _heroPotDef.onClick.AddListener(() => SpendPotential(m => m.TryBuyPotentialDef()));

            _statLine = BrothersUiUtil.MakeText(radarHost.transform, "Stats", "", 22, new Vector2(0, -230), new Vector2(420, 140));
            _statLine.alignment = TextAnchor.UpperCenter;
            _statLine.supportRichText = true;
            _statLine.raycastTarget = false;
            _statLine.color = new Color(0.92f, 0.88f, 0.76f);
            _statLine.gameObject.SetActive(false);

            LayoutRadarPluses();
            SetRadarPlusesVisible(false);
            _radarLayer.transform.SetAsLastSibling();
        }

        void SpendPotential(Func<MetaProgress, bool> buy)
        {
            if (_session?.Meta == null || buy == null) return;
            buy(_session.Meta);
            RefreshHubTexts();
        }

        void OnHubBack()
        {
            if (_pageStage != null && _pageStage.activeSelf)
            {
                SetRadarExpanded(false, instant: true);
                ShowHubPage(stage: false, animate: true);
                return;
            }

            _onHome?.Invoke();
        }

        static readonly Vector2 RadarCollapsedPos = new Vector2(430, 250);
        static readonly Vector2 RadarCollapsedSize = new Vector2(180, 180);
        static readonly Vector2 RadarExpandedPos = new Vector2(0, 40);
        static readonly Vector2 RadarExpandedSize = new Vector2(420, 560);

        void OnRadarClicked()
        {
            SetRadarExpanded(!_radarExpanded);
        }

        void SetRadarExpanded(bool on, bool instant = false)
        {
            _radarExpanded = on;
            if (_radarDimmer != null)
                _radarDimmer.gameObject.SetActive(on);
            if (_statLine != null)
                _statLine.gameObject.SetActive(on);
            SetRadarPlusesVisible(on);
            if (_radarLayer != null)
                _radarLayer.transform.SetAsLastSibling();
            if (_radarRoot != null)
                _radarRoot.SetAsLastSibling();

            if (_radarCo != null)
            {
                StopCoroutine(_radarCo);
                _radarCo = null;
            }

            if (instant || !isActiveAndEnabled)
            {
                ApplyRadarPose(on);
                return;
            }

            _radarCo = StartCoroutine(AnimateRadar(on));
        }

        void ApplyRadarCollapsed(bool instant) => SetRadarExpanded(false, instant);

        void ApplyRadarPose(bool expanded)
        {
            if (_radarRoot == null) return;
            _radarRoot.sizeDelta = expanded ? RadarExpandedSize : RadarCollapsedSize;
            _radarRoot.anchoredPosition = expanded ? RadarExpandedPos : RadarCollapsedPos;
            LayoutRadarPluses();
        }

        IEnumerator AnimateRadar(bool expanded)
        {
            if (_radarRoot == null) yield break;
            Vector2 fromPos = _radarRoot.anchoredPosition;
            Vector2 fromSize = _radarRoot.sizeDelta;
            Vector2 toPos = expanded ? RadarExpandedPos : RadarCollapsedPos;
            Vector2 toSize = expanded ? RadarExpandedSize : RadarCollapsedSize;
            const float dur = 0.22f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                _radarRoot.anchoredPosition = Vector2.Lerp(fromPos, toPos, k);
                _radarRoot.sizeDelta = Vector2.Lerp(fromSize, toSize, k);
                LayoutRadarPluses();
                yield return null;
            }

            ApplyRadarPose(expanded);
            _radarCo = null;
        }

        void LayoutRadarPluses()
        {
            PlacePlus(_heroPotHp, 0);
            PlacePlus(_heroPotAtk, 1);
            PlacePlus(_heroPotDef, 2);
        }

        void PlacePlus(Button btn, int axis)
        {
            if (btn == null || _radar == null) return;
            Vector2 radial = StatRadarGraphic.AxisDir(axis);
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            btn.GetComponent<RectTransform>().anchoredPosition =
                _radar.VertexLocal(axis, 82f) + tangent * 18f;
        }

        void SetRadarPlusesVisible(bool on)
        {
            if (_heroPotHp != null) _heroPotHp.gameObject.SetActive(on);
            if (_heroPotAtk != null) _heroPotAtk.gameObject.SetActive(on);
            if (_heroPotDef != null) _heroPotDef.gameObject.SetActive(on);
        }

        void BuildHeroPicker()
        {
            Transform content = _archivePanel != null ? _archivePanel.transform.Find("Content") : null;
            if (content == null) content = _archivePanel != null ? _archivePanel.transform : transform;
            var candidates = HeroUnlock.StarterCandidates();
            for (int i = 0; i < candidates.Count; i++)
            {
                var role = candidates[i];
                var cell = new GameObject("Hero" + i, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
                cell.transform.SetParent(content, false);
                var le = cell.GetComponent<LayoutElement>();
                le.preferredWidth = 118f;
                le.minWidth = 118f;
                le.preferredHeight = 148f;
                var frame = cell.GetComponent<Image>();
                frame.color = new Color(0.28f, 0.26f, 0.22f, 0.95f);
                var sp = RoleArtLoader.LoadPortrait(role.AvatarLoc);
                var img = BrothersUiUtil.MakePortrait(cell.transform, "Port", new Vector2(0, 14), new Vector2(96, 96),
                    sp, new Color(0.35f, 0.32f, 0.28f));
                var name = BrothersUiUtil.MakeText(cell.transform, "Name", role.Name, 20, new Vector2(0, -58), new Vector2(114, 28));
                name.color = BrothersUiUtil.Parchment;
                var lockTxt = BrothersUiUtil.MakeText(cell.transform, "Lock", "", 22, new Vector2(0, 14), new Vector2(96, 96));
                lockTxt.color = new Color(1f, 0.92f, 0.55f, 0.95f);
                string id = role.Id;
                cell.GetComponent<Button>().onClick.AddListener(() => OnPickHero(id));
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
            SetRadarExpanded(false);
            FocusHero(roleId);
        }

        void FocusHero(string roleId)
        {
            if (_session?.Meta == null) return;
            var role = RoleCatalog.FindRole(roleId);
            if (role == null) return;
            _detailRoleId = roleId;
            ApplyHeroDetail(role);
            bool can = HeroUnlock.CanSelect(roleId, _session.Meta);
            if (_detailSelect != null)
            {
                _detailSelect.gameObject.SetActive(true);
                _detailSelect.interactable = can;
                var label = _detailSelect.GetComponentInChildren<Text>();
                if (label != null) label.text = can ? "以此出征" : "未解锁";
                _detailSelect.GetComponent<Image>().color = can
                    ? BrothersUiUtil.AccentGreen
                    : new Color(0.35f, 0.33f, 0.3f, 1f);
            }

            _posePreview?.Show(RoleArtLoader.LoadBattleSet(role.BattleLoc, role.AvatarLoc), !can);
            RefreshHeroPicker();
        }

        void ApplyHeroDetail(RoleList role)
        {
            if (role == null || _session?.Meta == null) return;
            if (_detailName != null) _detailName.text = role.Name;
            if (_statLine != null)
            {
                _statLine.supportRichText = true;
                _statLine.text = HeroStatPreview.StatLines(role, _session.Meta);
            }

            if (_radar != null)
            {
                HeroStatPreview.FillRadar(role, _session.Meta, _radarInner, _radarOuter);
                _radar.SetValues(_radarInner, _radarOuter);
            }

            if (_detailHint != null) _detailHint.text = HeroDossier.Body(role, _session.Meta);
        }

        void OnConfirmHero()
        {
            if (_session?.Meta == null || string.IsNullOrEmpty(_detailRoleId)) return;
            if (!_session.Meta.TrySelectHero(_detailRoleId)) return;
            SetRadarExpanded(false, instant: true);
            RefreshHeroPicker();
            ShowHubPage(stage: true, animate: true);
        }

        void RefreshHeroPicker()
        {
            if (_session?.Meta == null) return;
            if (_heroSlots.Count == 0)
                BuildHeroPicker();
            var meta = _session.Meta;
            string focused = string.IsNullOrEmpty(_detailRoleId) ? meta.SelectedHeroId : _detailRoleId;
            RoleList focusedRole = null;
            foreach (var slot in _heroSlots)
            {
                var role = RoleCatalog.FindRole(slot.Id);
                if (role == null) continue;
                bool unlocked = HeroUnlock.IsUnlocked(role, meta);
                bool isFocused = slot.Id == focused;
                if (isFocused) focusedRole = role;
                slot.Portrait.color = unlocked ? Color.white : new Color(0.45f, 0.45f, 0.48f, 0.9f);
                slot.Frame.color = isFocused
                    ? new Color(0.92f, 0.74f, 0.28f, 1f)
                    : unlocked
                        ? new Color(0.32f, 0.42f, 0.34f, 0.95f)
                        : new Color(0.22f, 0.2f, 0.2f, 0.9f);
                slot.Lock.text = unlocked ? "" : "锁";
                slot.Name.color = isFocused ? new Color(1f, 0.86f, 0.4f) : BrothersUiUtil.Parchment;
            }

            if (_archiveHint != null)
            {
                _archiveHint.text = focusedRole != null
                    ? (HeroUnlock.IsUnlocked(focusedRole, meta)
                        ? $"当前：{focusedRole.Name} · 蓝初始 / 绿培养"
                        : $"未解锁：{focusedRole.Name} · 蓝初始 / 绿培养")
                    : "左右滑选人 · 蓝=初始 绿=培养";
            }

            if (string.IsNullOrEmpty(_detailRoleId) && focusedRole != null)
                FocusHero(focusedRole.Id);
            else if (focusedRole != null)
                ApplyHeroDetail(focusedRole);
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

            var quit = BrothersUiUtil.MakeButton(_battleHud.transform, "BtnQuit", "返回", new Vector2(-430, 860), new Vector2(160, 64),
                new Color(0.45f, 0.22f, 0.22f));
            _btnBattleBack = quit;
            quit.onClick.AddListener(() => _session?.ReturnToHub());
        }

        void BuildOverlay()
        {
            var panel = BrothersUiUtil.MakePanel(transform, "Overlay", new Vector2(1080, 1920), Vector2.zero,
                new Color(0.08f, 0.07f, 0.06f, 0.96f));
            _overlay = panel.gameObject;

            _overlayTitle = BrothersUiUtil.MakeText(_overlay.transform, "Title", "", 52, new Vector2(0, 820), new Vector2(960, 80));
            _overlayTitle.color = BrothersUiUtil.Parchment;
            _btnOverlayBack = BrothersUiUtil.MakeButton(_overlay.transform, "Back", "返回", new Vector2(-430, 860), new Vector2(160, 64),
                new Color(0.32f, 0.32f, 0.36f));
            _btnOverlayBack.onClick.AddListener(() => _session?.ReturnToHub());
            _overlayPortrait = BrothersUiUtil.MakePortrait(_overlay.transform, "WinPort", new Vector2(0, 560), new Vector2(220, 280),
                null, new Color(0.16f, 0.14f, 0.12f));
            _overlayBody = BrothersUiUtil.MakeText(_overlay.transform, "Body", "", 26, new Vector2(0, 320), new Vector2(920, 180));
            _overlayBody.color = new Color(0.92f, 0.88f, 0.78f);
            _overlayShare = BrothersUiUtil.MakeText(_overlay.transform, "Share", "", 24, new Vector2(0, 180), new Vector2(900, 80));
            _overlayShare.color = new Color(0.75f, 0.85f, 1f);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                float x = (i - 1) * 330f;
                var frame = BrothersUiUtil.MakePanel(_overlay.transform, "Rogue" + i, new Vector2(300, 760), new Vector2(x, -40),
                    new Color(0.16f, 0.14f, 0.12f, 1f));
                var art = BrothersUiUtil.MakePortrait(frame.transform, "Art", new Vector2(0, 170), new Vector2(260, 340),
                    null, new Color(0.28f, 0.24f, 0.18f));
                var kind = BrothersUiUtil.MakeText(frame.transform, "Kind", "", 20, new Vector2(0, -20), new Vector2(260, 32));
                kind.color = new Color(0.95f, 0.78f, 0.4f);
                var title = BrothersUiUtil.MakeText(frame.transform, "Title", "", 26, new Vector2(0, -70), new Vector2(270, 70));
                title.color = BrothersUiUtil.Parchment;
                var desc = BrothersUiUtil.MakeText(frame.transform, "Desc", "", 20, new Vector2(0, -230), new Vector2(270, 220));
                desc.alignment = TextAnchor.UpperCenter;
                desc.color = new Color(0.9f, 0.84f, 0.72f);
                var btn = frame.gameObject.AddComponent<Button>();
                btn.targetGraphic = frame;
                btn.onClick.AddListener(() => _session?.PickRogue(idx));
                _rogueCards[i] = new RogueCardUi
                {
                    Root = frame.gameObject,
                    Button = btn,
                    Frame = frame,
                    Art = art,
                    Kind = kind,
                    Title = title,
                    Desc = desc
                };
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
                new Vector2(-205, -720), new Vector2(380, 72), new Color(0.72f, 0.42f, 0.18f));
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
                new Vector2(205, -720), new Vector2(380, 72), new Color(0.72f, 0.42f, 0.18f));
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
            if (_metaText != null)
            {
                _metaText.text =
                    $"体力 {m.Stamina}/{GameTables.DailyStaminaCap}    培养点 {m.TrainPoints}    人情 {m.Favor}    声望 {m.Renown}";
            }

            if (_trainHint != null)
            {
                _trainHint.text =
                    $"潜力 生命+{m.PotentialHp} 攻击+{m.PotentialAtk} 防御+{m.PotentialDef} · 养伤档 {m.HealShortenTier}\n" +
                    "蓝=人物初始 · 绿=培养增幅 · 开局带入战斗";
            }

            bool can3 = m.TrainPoints >= 3;
            bool can5 = m.TrainPoints >= 5 && m.HealShortenTier < 3;
            var hpCol = new Color(0.28f, 0.45f, 0.55f);
            var atkCol = new Color(0.55f, 0.32f, 0.28f);
            var defCol = new Color(0.32f, 0.4f, 0.52f);
            var healCol = new Color(0.35f, 0.5f, 0.35f);
            BrothersUiUtil.SetAffordable(_btnPotHp, can3, hpCol);
            BrothersUiUtil.SetAffordable(_btnPotAtk, can3, atkCol);
            BrothersUiUtil.SetAffordable(_btnPotDef, can3, defCol);
            BrothersUiUtil.SetAffordable(_btnHeal, can5, healCol);
            BrothersUiUtil.SetAffordable(_heroPotHp, can3, BrothersUiUtil.PlusOrange);
            BrothersUiUtil.SetAffordable(_heroPotAtk, can3, BrothersUiUtil.PlusOrange);
            BrothersUiUtil.SetAffordable(_heroPotDef, can3, BrothersUiUtil.PlusOrange);

            var ch = GameTables.FindChapter(ChapterId.Primary);
            var hero = RoleCatalog.FindRole(_session.Meta.SelectedHeroId);
            if (_chapterText != null)
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
            ShowHubPage(stage: false, animate: false);
        }

        void ShowHubPage(bool stage, bool animate)
        {
            if (stage)
                SetRadarExpanded(false, instant: true);
            if (_radarRoot != null)
                _radarRoot.gameObject.SetActive(!stage);
            if (_slideCo != null)
            {
                StopCoroutine(_slideCo);
                _slideCo = null;
            }

            if (!animate || _pageHero == null || _pageStage == null)
            {
                _pageHero.SetActive(!stage);
                _pageStage.SetActive(stage);
                return;
            }

            _slideCo = StartCoroutine(SlideHub(stage));
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
            if (_radarRoot != null)
                _radarRoot.gameObject.SetActive(!toStage);
            _slideCo = null;
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
            SetOverlayBack(false);
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
            SetOverlayBack(false);
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
            SetOverlayBack(true);
            _overlayTitle.text = "三选一";
            if (_overlayPortrait != null) _overlayPortrait.gameObject.SetActive(false);
            if (_session.Run != null)
            {
                var run = _session.Run;
                string slots = run.HasUnlimitedSlots ? "不限人数" : $"{run.RecruitedCount}/{run.ActiveSlotLimit}人";
                var timeline = TimelineCatalog.Current(run);
                string eventLine = timeline == null ? "" : $" · {timeline.Title}";
                _overlayBody.text = $"{run.PhaseLabel()} · {slots}{eventLine}";
            }
            else
            {
                _overlayBody.text = "";
            }
            _overlayShare.text = "";
            _overlayBody.rectTransform.anchoredPosition = new Vector2(0f, 700f);
            _overlayBody.rectTransform.sizeDelta = new Vector2(960f, 70f);
            _btnAdReroll.gameObject.SetActive(_session.CanRewardedReroll);
            _btnAdExpand.gameObject.SetActive(_session.CanRewardedExpand);

            var opts = _session.CurrentRogue;
            for (int i = 0; i < 3; i++)
            {
                var card = _rogueCards[i];
                if (card?.Root == null) continue;
                if (i < opts.Count)
                {
                    var opt = opts[i];
                    card.Root.SetActive(true);
                    card.Kind.text = RogueKindLabel(opt.Kind);
                    card.Title.text = opt.Title;
                    card.Desc.text = opt.Desc;
                    card.Frame.color = RogueKindColor(opt.Kind);
                    string portraitLoc = null;
                    if (!string.IsNullOrEmpty(opt.TargetRoleId))
                        portraitLoc = RoleCatalog.FindRole(opt.TargetRoleId)?.AvatarLoc;
                    var art = RoleArtLoader.LoadRogueIcon(opt.Kind.ToString(), portraitLoc);
                    card.Art.sprite = art;
                    card.Art.preserveAspect = false;
                    card.Art.color = art != null ? Color.white : RogueKindColor(opt.Kind);
                }
                else
                {
                    card.Root.SetActive(false);
                }
            }
        }

        static string RogueKindLabel(ERogueRewardKind kind)
        {
            return kind switch
            {
                ERogueRewardKind.Stat => "属性",
                ERogueRewardKind.Recovery => "回复",
                ERogueRewardKind.TeamBuff => "下一波",
                ERogueRewardKind.CampusSkill => "课间绝活",
                ERogueRewardKind.JobSkill => "工牌绝活",
                ERogueRewardKind.Encounter => "相遇",
                ERogueRewardKind.Equipment => "攻击核心",
                ERogueRewardKind.LootSkill => "地摊技",
                ERogueRewardKind.Event => "糗事",
                _ => kind.ToString()
            };
        }

        static Color RogueKindColor(ERogueRewardKind kind)
        {
            return kind switch
            {
                ERogueRewardKind.Stat => new Color(0.42f, 0.28f, 0.16f, 1f),
                ERogueRewardKind.Recovery => new Color(0.18f, 0.36f, 0.24f, 1f),
                ERogueRewardKind.TeamBuff => new Color(0.42f, 0.2f, 0.2f, 1f),
                ERogueRewardKind.CampusSkill => new Color(0.18f, 0.28f, 0.42f, 1f),
                ERogueRewardKind.JobSkill => new Color(0.4f, 0.34f, 0.14f, 1f),
                ERogueRewardKind.Encounter => new Color(0.34f, 0.2f, 0.38f, 1f),
                ERogueRewardKind.Equipment => new Color(0.2f, 0.24f, 0.36f, 1f),
                ERogueRewardKind.LootSkill => new Color(0.36f, 0.26f, 0.16f, 1f),
                ERogueRewardKind.Event => new Color(0.4f, 0.18f, 0.22f, 1f),
                _ => new Color(0.16f, 0.14f, 0.12f, 1f)
            };
        }

        void ShowBreak()
        {
            _hub.SetActive(false);
            _battleHud.SetActive(false);
            _overlay.SetActive(true);
            SetRogueVisible(false);
            _btnConfirm.gameObject.SetActive(false);
            _btnRetry.gameObject.SetActive(false);
            _btnHome.gameObject.SetActive(false);
            _btnShare.gameObject.SetActive(false);
            _btnBreakContinue.gameObject.SetActive(true);
            _btnAdTrain.gameObject.SetActive(true);
            SetOverlayBack(true);
            _overlayTitle.text = "假期休整";
            if (_overlayPortrait != null) _overlayPortrait.gameObject.SetActive(false);
            _overlayBody.text = $"{_session.LastSettleDetail}\n{_session.NextNodeHint}\n\n{_session.Run?.PhaseLabel()}";
            _overlayShare.text = "";
        }

        void SetOverlayBack(bool on)
        {
            if (_btnOverlayBack != null)
                _btnOverlayBack.gameObject.SetActive(on);
        }

        void SetRogueVisible(bool on)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_rogueCards[i]?.Root != null)
                    _rogueCards[i].Root.SetActive(on);
            }

            if (!on && _overlayBody != null)
            {
                _overlayBody.rectTransform.anchoredPosition = new Vector2(0f, 320f);
                _overlayBody.rectTransform.sizeDelta = new Vector2(920f, 180f);
            }
        }
    }
}
