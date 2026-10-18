using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MiCay.UI
{
    public partial class MainUI : MonoBehaviour
    {
        GameManager gm;
        RectTransform safe, gameRoot, modalLayer, toastLayer, titleLayer;
        public DialogueUI Dialogue { get; private set; }

        Text dayText, clockText, cashText, chapterText, goalText;
        readonly Image[] stars = new Image[5];
        Button taxBtn, closeBtn;

        RectTransform scene, world, tablesLayer;
        ScrollRect sceneScroll;
        Button leftArrow, rightArrow;
        float scrollTarget = -1f;
        readonly RectTransform[] slots = new RectTransform[CustomerSystem.MaxTables];
        Image anhImg, vyImg, counterImg, steamImg;
        static readonly float[] FrontHelperX = { 0.71f, 0.88f, 0.15f };
        readonly Image[] frontHelperImgs = new Image[FrontHelperX.Length];
        readonly string[] frontHelperNames = new string[FrontHelperX.Length];
        RectTransform walkersLayer;
        readonly List<(RectTransform rt, CustomerGroup g)> customerWalkers = new List<(RectTransform, CustomerGroup)>();
        readonly List<(RectTransform rt, Waiter w)> waiterWalkers = new List<(RectTransform, Waiter)>();
        readonly List<(RectTransform rt, Leaver l)> leaverWalkers = new List<(RectTransform, Leaver)>();
        readonly Dictionary<int, Image> eatBars = new Dictionary<int, Image>();
        RectTransform counterFitRt;
        const float StaffW = 22f / 128f;
        const float StaffFeetY = 1f + 3f / 44f - 32f / 44f;
        static float StaffBaseX(string id) => id == "vy" ? 0.52f : 0.33f;
        readonly Text[] chatTexts = new Text[1];
        readonly Image[] chatPortraits = new Image[1];
        RectTransform tutStrip;
        Text tutStripText;
        const float ChatH = 0.058f, TutH = 0.105f;
        float prepBottom = -1f;
        RectTransform prepOverlay;
        Text prepText;
        Button openBtn;
        RectTransform tutorialBox;
        Text tutorialText;
        Button marketBtn, clearPotBtn, menuBtn, serviceBtn, financeBtn;
        Image nightSky, nightWorld;
        Text emptyShelfText, shelfTopLabel, shelfBottomLabel;
        readonly Dictionary<int, RectTransform> orderCells = new Dictionary<int, RectTransform>();
        readonly Dictionary<int, RectTransform> serveBtns = new Dictionary<int, RectTransform>();
        readonly Dictionary<string, RectTransform> ingCards = new Dictionary<string, RectTransform>();
        float prepTimer;
        readonly Dictionary<int, Image> patienceBars = new Dictionary<int, Image>();
        readonly List<(RectTransform rt, Vector2 basePos, float phase)> bobbers = new List<(RectTransform, Vector2, float)>();

        RectTransform stationRow, potRow, ingGridTop, ingGridBottom;
        Button foodShelfBtn, drinkShelfBtn;
        bool viewDrinks;
        int viewTarget = -1;
        const float StationGap = 6f;
        const int StoveCols = 3;
        Image targetIcon;
        Text targetText, hintText;
        Button quickBtn, hintBtn, cookBtn;
        readonly List<(Station st, Image fill, Image fire, Text label)> stationViews = new List<(Station, Image, Image, Text)>();

        readonly List<(GameObject go, float t)> toasts = new List<(GameObject, float)>();
        float animTimer;
        int animFrame;

        GameState S => gm.State;

        static readonly Color Yellow = new Color(1f, 0.85f, 0.35f);

        public void Build(GameManager manager)
        {
            gm = manager;
            var cgo = new GameObject("Canvas", typeof(RectTransform)) { layer = 5 };
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = false;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();

            UIKit.Box(cgo.transform, new Color(0.16f, 0.11f, 0.09f), false, "Background").rectTransform.Fill();
            safe = UIKit.NewRect("Safe", cgo.transform).Fill();
            safe.gameObject.AddComponent<SafeArea>();

            gameRoot = UIKit.NewRect("Game", safe).Fill();
            BuildScene();
            BuildKitchen();
            BuildHud();

            titleLayer = UIKit.NewRect("Title", safe).Fill();
            modalLayer = UIKit.NewRect("Modals", safe).Fill();
            BuildPointer();

            var dl = UIKit.NewRect("DialogueLayer", safe).Fill();
            Dialogue = dl.gameObject.AddComponent<DialogueUI>();
            Dialogue.Build(dl);

            toastLayer = UIKit.NewRect("Toasts", safe).Anchors(0.02f, 0.004f, 0.98f, 0.037f);

            gm.Changed += OnChanged;
            gm.ToastRaised += ShowToast;
        }

        bool refreshPending;
        readonly string[] slotKeys = new string[CustomerSystem.MaxTables];
        string ingKey;

        static bool PointerHeld()
        {
            var p = UnityEngine.InputSystem.Pointer.current;
            return p != null && p.press.isPressed;
        }

        void OnChanged()
        {
            if (PointerHeld()) { refreshPending = true; return; }
            Refresh();
        }

        void LateUpdate()
        {
            if (refreshPending && !PointerHeld()) Refresh();
            UpdatePointer();
        }

        Image DarkBox(Transform parent, string name, float alpha = 1f)
        {
            var img = UIKit.Box(parent, new Color(1, 1, 1, alpha), false, name);
            Art.Sliced(img, Art.Dark);
            if (Art.Dark == null) img.color = new Color(0.16f, 0.11f, 0.09f, alpha);
            return img;
        }

        void BuildHud()
        {
            var bar = DarkBox(gameRoot, "TopBar");
            bar.rectTransform.Anchors(0, 0.915f, 1, 1);
            UIKit.HList(bar, 10, 10);

            var dayCol = UIKit.NewRect("Day", bar.transform);
            dayCol.Size(w: 200);
            dayText = UIKit.Label(dayCol, "", 30, Yellow, TextAnchor.UpperLeft);
            dayText.rectTransform.Anchors(0, 0.45f, 1, 1);
            var starRow = UIKit.NewRect("Stars", dayCol).Anchors(0, 0, 1, 0.45f);
            var sh = UIKit.HList(starRow, 2, 0);
            sh.childControlWidth = false; sh.childControlHeight = false;
            for (int i = 0; i < 5; i++)
            {
                stars[i] = Art.Pixel(starRow, Art.Get("ui/star_full"), 2.6f, "Star");
            }

            var clockBox = UIKit.NewRect("Clock", bar.transform);
            clockBox.Size(w: 170);
            var ci = Art.Pixel(clockBox, Art.Get("ui/clock"), 3.4f, "Icon");
            ci.rectTransform.anchorMin = ci.rectTransform.anchorMax = new Vector2(0, 0.5f);
            ci.rectTransform.pivot = new Vector2(0, 0.5f);
            clockText = UIKit.Label(clockBox, "", 36, Color.white);
            clockText.rectTransform.Anchors(0.32f, 0, 1, 1);

            var cashBox = UIKit.Box(bar.transform, new Color(0.25f, 0.18f, 0.12f), true, "Cash");
            cashBox.Size(w: 330);
            var coin = Art.Pixel(cashBox.transform, Art.Get("ui/coin"), 3.6f, "Coin");
            coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0, 0.5f);
            coin.rectTransform.pivot = new Vector2(0, 0.5f);
            coin.rectTransform.anchoredPosition = new Vector2(10, 0);
            cashText = UIKit.Label(cashBox.transform, "", 36, Yellow, TextAnchor.MiddleRight);
            cashText.rectTransform.Anchors(0.2f, 0, 0.96f, 1);

            chapterText = UIKit.Label(bar.transform, "", 24, new Color(1, 1, 1, 0.85f));
            chapterText.Size(w: 160, flexW: 1);

            marketBtn = UIKit.Button(bar.transform, "Nhập hàng", OpenMarket, Pal.Orange, 26);
            marketBtn.Size(w: 150);
            menuBtn = UIKit.Button(bar.transform, "Thực đơn", OpenMenu, Pal.Teal, 26);
            menuBtn.Size(w: 140);
            serviceBtn = UIKit.Button(bar.transform, "Dịch vụ", OpenServices, new Color(0.55f, 0.3f, 0.65f), 26);
            serviceBtn.Size(w: 130);
            financeBtn = UIKit.Button(bar.transform, "Sổ sách", () => { OpenFinance(0); gm.TutorialSignal("finance"); }, Pal.Blue, 26);
            financeBtn.Size(w: 130);
            taxBtn = UIKit.Button(bar.transform, "Thuế", OpenTax, Pal.Green, 26);
            taxBtn.Size(w: 120);
            UIKit.Button(bar.transform, "II", OpenPauseMenu, Pal.WoodDark, 28).Size(w: 80);

            var strip = DarkBox(gameRoot, "ReviewStrip", 0.92f);
            strip.rectTransform.Anchors(0, 0.875f, 1, 0.917f);
            var stripBtn = strip.gameObject.AddComponent<Button>();
            stripBtn.targetGraphic = strip;
            stripBtn.onClick.AddListener(() => { Sfx.Play("click"); OpenReviews(); });
            goalText = UIKit.Label(strip.transform, "", 22, Color.white);
            goalText.rectTransform.Anchors(0.01f, 0, 0.84f, 1);
            closeBtn = UIKit.Button(strip.transform, "Đóng cửa", () =>
                ShowConfirm("Đóng cửa sớm?", "Khách đang ngồi sẽ phải về và đánh giá 1 sao. Kết thúc ngày ngay?", "Đóng cửa", gm.CloseNow), Pal.Red, 22);
            ((RectTransform)closeBtn.transform).Anchors(0.86f, 0.06f, 0.995f, 0.94f);
        }

        void BuildScene()
        {
            var sceneBg = UIKit.Box(gameRoot, new Color(0.58f, 0.72f, 0.85f), false, "Scene");
            scene = sceneBg.rectTransform.Anchors(0, 0, 0.58f, 0.875f);
            scene.gameObject.AddComponent<RectMask2D>();

            world = UIKit.NewRect("World", scene);
            world.anchorMin = Vector2.zero;
            world.anchorMax = new Vector2(CustomerSystem.WorldScreens, 1);
            world.pivot = new Vector2(0, 0.5f);
            world.offsetMin = world.offsetMax = Vector2.zero;
            sceneScroll = scene.gameObject.AddComponent<ScrollRect>();
            sceneScroll.content = world;
            sceneScroll.viewport = scene;
            sceneScroll.horizontal = true;
            sceneScroll.vertical = false;
            sceneScroll.movementType = ScrollRect.MovementType.Clamped;
            sceneScroll.decelerationRate = 0.08f;
            sceneScroll.scrollSensitivity = 60;
            sceneScroll.onValueChanged.AddListener(_ => UpdateArrows());

            var sky = Art.Tiled(world, Art.Get("scene/city", 0, 7f), "City").rectTransform;
            sky.anchorMin = new Vector2(0, 1);
            sky.anchorMax = new Vector2(1, 1);
            sky.pivot = new Vector2(0.5f, 1);
            sky.sizeDelta = new Vector2(0, 72 * 7f);
            sky.anchoredPosition = Vector2.zero;
            nightSky = UIKit.Box(world, Color.clear, false, "NightSky");
            nightSky.rectTransform.Anchors(0, 0.5f, 1, 1);
            nightSky.raycastTarget = false;
            Art.Tiled(world, Art.Get("scene/floor"), "Floor").rectTransform.Anchors(0, 0, 1, 0.56f);

            float half = 1f / CustomerSystem.WorldScreens;
            string[] areaNames = { GameManager.ShopName, "KHU MỞ RỘNG", "SÂN SAU", "KHU VỈA HÈ" };
            Color[] areaTints = { Color.white, Color.white, new Color(0.82f, 1f, 0.82f), new Color(1f, 0.88f, 0.78f) };
            for (int a = 0; a < CustomerSystem.Areas; a++)
                Facade(world, a == 0 ? "scene/facade" : "scene/annex", areaNames[Mathf.Min(a, areaNames.Length - 1)], a * half, (a + 1) * half,
                    a == 0 ? Yellow : Color.white, a == 0 ? new Color(0.3f, 0.05f, 0.02f) : new Color(0.05f, 0.25f, 0.12f), areaTints[Mathf.Min(a, areaTints.Length - 1)]);
            nightWorld = UIKit.Box(world, Color.clear, false, "NightWorld");
            nightWorld.rectTransform.Fill();
            nightWorld.raycastTarget = false;

            var counterBox = UIKit.NewRect("CounterBox", world).Anchors(0.16f * half, 0.55f, 0.84f * half, 0.72f);
            var counterFit = UIKit.NewRect("CounterFit", counterBox);
            counterFitRt = counterFit;
            var cf = counterFit.gameObject.AddComponent<AspectRatioFitter>();
            cf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            cf.aspectRatio = 128f / 44f;
            anhImg = StaffSprite(counterFit, "anh", StaffBaseX("anh"));
            vyImg = StaffSprite(counterFit, "vy", StaffBaseX("vy"));
            Bob(anhImg.rectTransform, 0f);
            Bob(vyImg.rectTransform, 1.3f);
            for (int i = 0; i < frontHelperImgs.Length; i++)
            {
                frontHelperImgs[i] = StaffSprite(counterFit, "nv1", FrontHelperX[i]);
                frontHelperImgs[i].enabled = false;
                Bob(frontHelperImgs[i].rectTransform, 0.6f + 0.7f * i);
            }
            counterImg = UIKit.NewRect("Counter", counterFit).Fill().gameObject.AddComponent<Image>();
            counterImg.sprite = Art.Get("scene/counter");
            counterImg.raycastTarget = false;
            steamImg = UIKit.NewRect("Steam", counterFit).Anchors(0.06f, 0.88f, 0.18f, 1.25f).gameObject.AddComponent<Image>();
            steamImg.sprite = Art.Get("scene/steam0");
            steamImg.preserveAspect = true;
            steamImg.raycastTarget = false;

            tablesLayer = UIKit.NewRect("Tables", world).Fill();
            walkersLayer = UIKit.NewRect("Walkers", world).Fill();
            foreach (int i in Enumerable.Range(0, slots.Length).OrderByDescending(CustomerSystem.TableY))
            {
                var rt = UIKit.NewRect("Ban" + (i + 1), tablesLayer);
                rt.anchorMin = rt.anchorMax = new Vector2(CustomerSystem.TableX(i), CustomerSystem.TableY(i));
                rt.pivot = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(270, 330);
                slots[i] = rt;
            }

            leftArrow = UIKit.Button(scene, "<", () => PageArea(-1), Pal.WoodDark, 34);
            TopArrow((RectTransform)leftArrow.transform, 0.006f, 0.07f);
            rightArrow = UIKit.Button(scene, ">", () => PageArea(1), Pal.WoodDark, 34);
            TopArrow((RectTransform)rightArrow.transform, 0.93f, 0.994f);

            var ov = UIKit.Box(scene, Color.white, true, "PrepOverlay");
            Art.Sliced(ov, Art.Paper);
            prepOverlay = ov.rectTransform.Anchors(0.08f, 0.1f, 0.92f, 0.86f);
            prepText = UIKit.Label(prepOverlay, "", 27, Pal.Text, TextAnchor.UpperLeft);
            prepText.rectTransform.Anchors(0.05f, 0.24f, 0.95f, 0.95f);
            prepText.Fit(27, 13);
            openBtn = UIKit.Button(prepOverlay, "MỞ CỬA BÁN HÀNG", OnOpenPressed, Pal.Red, 44);
            ((RectTransform)openBtn.transform).Anchors(0.2f, 0.05f, 0.8f, 0.2f);

            var chat = DarkBox(scene, "Chat", 0.95f);
            chat.rectTransform.Anchors(0, 0, 1, ChatH);
            for (int i = 0; i < chatTexts.Length; i++)
            {
                float rh = 1f / chatTexts.Length;
                var row = UIKit.NewRect("Row" + i, chat.transform).Anchors(0, i * rh, 1, (i + 1) * rh);
                var pic = Art.Pixel(row, null, 1f, "Portrait");
                var prt = pic.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f);
                prt.pivot = new Vector2(0, 0.5f);
                prt.anchoredPosition = new Vector2(10, 0);
                prt.sizeDelta = new Vector2(50, 44);
                chatPortraits[i] = pic;
                var txt = UIKit.Label(row, "", 26, Color.white);
                txt.rectTransform.Anchors(0.065f, 0, 0.995f, 1);
                txt.Fit(26, 12);
                chatTexts[i] = txt;
            }

            var strip = UIKit.Box(scene, new Color(1f, 0.95f, 0.6f, 0.97f), true, "TutorialStrip");
            strip.raycastTarget = false;
            var so = strip.gameObject.AddComponent<Outline>();
            so.effectColor = new Color(0.75f, 0.2f, 0.1f);
            so.effectDistance = new Vector2(3, -3);
            tutStrip = strip.rectTransform.Anchors(0.004f, ChatH + 0.005f, 0.996f, ChatH + TutH);
            tutStripText = UIKit.Label(strip.transform, "", 24, Pal.Text, TextAnchor.MiddleLeft);
            tutStripText.rectTransform.Anchors(0.012f, 0.04f, 0.988f, 0.96f);
            tutStripText.Fit(24, 12);
            tutStrip.gameObject.SetActive(false);
        }

        static readonly GameManager.ChatLine IdleChat = new GameManager.ChatLine { name = "Ánh", text = "Hôm nay bán đắt hàng nha!" };

        void RefreshChat()
        {
            var log = gm.ChatLog;
            for (int i = 0; i < chatTexts.Length; i++)
            {
                var line = log.Count > i ? log[log.Count - 1 - i] : i == 0 ? IdleChat : null;
                bool show = line != null;
                if (chatTexts[i].gameObject.activeSelf != show) chatTexts[i].gameObject.SetActive(show);
                chatPortraits[i].enabled = show;
                if (!show) continue;
                bool old = i > 0;
                chatTexts[i].Set(line.notice
                    ? $"<color={(old ? "#8FD3FFA0" : "#8FD3FF")}>Thông báo:</color> {line.text}"
                    : $"<color={(old ? "#FFD45AA0" : "#FFD45A")}>{line.name}:</color> {line.text}");
                chatTexts[i].color = old ? new Color(1, 1, 1, 0.62f) : Color.white;
                chatPortraits[i].sprite = line.notice ? NoticeSprite() : line.look >= 0 ? Art.Portrait(Art.CustomerId(line.look)) : Art.Portrait("anh");
                chatPortraits[i].color = old ? new Color(1, 1, 1, 0.62f) : Color.white;
            }
        }

        static Sprite noticeSprite;

        static Sprite NoticeSprite()
        {
            if (noticeSprite != null) return noticeSprite;
            string[] rows =
            {
                "..OOOOO..",
                ".OYYYYYO.",
                "OYYYOYYYO",
                "OYYYOYYYO",
                "OYYYOYYYO",
                "OYYYYYYYO",
                "OYYYOYYYO",
                ".OYYYYYO.",
                "..OOOOO..",
            };
            var ink = new Color32(70, 35, 20, 255);
            var gold = new Color32(255, 214, 90, 255);
            var tex = new Texture2D(9, 9, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[81];
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 9; x++)
            {
                char c = rows[8 - y][x];
                px[y * 9 + x] = c == 'O' ? ink : c == 'Y' ? gold : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            noticeSprite = Sprite.Create(tex, new Rect(0, 0, 9, 9), new Vector2(0.5f, 0.5f), 100f);
            return noticeSprite;
        }

        static void TopArrow(RectTransform rt, float x0, float x1)
        {
            rt.anchorMin = new Vector2(x0, 1);
            rt.anchorMax = new Vector2(x1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 130);
            rt.anchoredPosition = new Vector2(0, -12);
        }

        void Facade(RectTransform parent, string sprite, string signText, float x0, float x1, Color textColor, Color shadowColor, Color? tint = null)
        {
            var box = UIKit.NewRect("FacadeBox", parent).Anchors(x0, 0.52f, x1, 0.995f);
            var facade = Art.Fit(box, Art.Get(sprite), false, "Facade");
            if (tint.HasValue) facade.color = tint.Value;
            var sign = UIKit.Label(facade.transform, signText, 56, textColor, TextAnchor.MiddleCenter);
            sign.rectTransform.Anchors(0.12f, 0.65f, 0.88f, 0.785f);
            sign.Fit(56, 20);
            var so = sign.gameObject.AddComponent<Shadow>();
            so.effectColor = shadowColor;
            so.effectDistance = new Vector2(3, -3);
        }

        void PageArea(int dir)
        {
            int pages = CustomerSystem.Areas;
            if (pages <= 1) return;
            float cur = (scrollTarget >= 0f ? scrollTarget : sceneScroll.horizontalNormalizedPosition) * (pages - 1);
            int p = Mathf.Clamp(Mathf.RoundToInt(cur) + dir, 0, pages - 1);
            scrollTarget = p / (float)(pages - 1);
            gm.TutorialSignal(dir > 0 ? "right" : "left");
        }

        (float min, float max) VisibleRange()
        {
            float vis = 1f / CustomerSystem.WorldScreens;
            float min = sceneScroll.horizontalNormalizedPosition * (1f - vis);
            return (min, min + vis);
        }

        void UpdateArrows()
        {
            if (sceneScroll == null || gm == null) return;
            var (min, max) = VisibleRange();
            int left = 0, right = 0;
            foreach (var g in gm.Customers.groups)
            {
                if (!g.Arrived || g.serving) continue;
                float x = CustomerSystem.TableX(g.table);
                if (x < min) left++;
                else if (x > max) right++;
            }
            SetArrow(leftArrow, "<", left, min > 0.01f);
            SetArrow(rightArrow, ">", right, max < 0.99f);
        }

        static void SetArrow(Button b, string arrow, int count, bool show)
        {
            b.gameObject.SetActive(show);
            b.Caption().text = count > 0 ? $"{arrow}\n{count}" : arrow;
            ((Image)b.targetGraphic).color = count > 0 ? Pal.Red : Pal.WoodDark;
        }

        void ToggleSound()
        {
            Sfx.Enabled = !Sfx.Enabled;
            Sfx.Play("click");
        }

        Image StaffSprite(RectTransform counterFit, string id, float xCenter)
        {
            var img = UIKit.NewRect(id, counterFit).gameObject.AddComponent<Image>();
            img.sprite = Art.Char(id);
            img.raycastTarget = false;
            float w = 22f / 128f, top = 1f + 3f / 44f, h = 32f / 44f;
            img.rectTransform.Anchors(xCenter - w / 2, top - h, xCenter + w / 2, top);
            return img;
        }

        void Bob(RectTransform rt, float phase) => bobbers.Add((rt, rt.anchoredPosition, phase));

        void OnOpenPressed()
        {
            if (S.closedDay == S.day) gm.CloseNow();
            else gm.OpenShop();
        }

        void BuildKitchen()
        {
            var panel = UIKit.Box(gameRoot, Pal.Wood, false, "Kitchen");
            Art.Sliced(panel, Art.WoodPanel);
            if (Art.WoodPanel != null) panel.color = Color.white;
            var k = panel.rectTransform.Anchors(0.58f, 0, 1, 0.875f);
            Art.Tiled(k, Art.WoodTile, "Stripes").rectTransform.Fill(30);

            stationRow = UIKit.NewRect("Stations", k).Anchors(0.03f, 0.735f, 0.97f, 0.975f);

            var card = UIKit.Box(k, Color.white, true, "Order");
            Art.Sliced(card, Art.Card);
            card.rectTransform.Anchors(0.03f, 0.5f, 0.97f, 0.728f);
            targetIcon = Art.Pixel(card.transform, null, 3f, "Icon");
            targetIcon.rectTransform.Anchors(0.015f, 0.42f, 0.14f, 0.95f);
            targetText = UIKit.Label(card.transform, "", 26, Pal.Text);
            targetText.rectTransform.Anchors(0.155f, 0.74f, 0.985f, 0.96f);
            hintText = UIKit.Label(card.transform, "", 21, new Color(0.42f, 0.28f, 0.18f), TextAnchor.UpperLeft);
            hintText.rectTransform.Anchors(0.155f, 0.42f, 0.985f, 0.74f);
            hintText.Fit(21, 12);
            potRow = UIKit.NewRect("Pot", card.transform).Anchors(0.015f, 0.04f, 0.985f, 0.4f);
            var ph = UIKit.HList(potRow, 4, 2);
            ph.childControlWidth = false;
            ph.childControlHeight = false;
            ph.childForceExpandWidth = false;

            var shelf = UIKit.NewRect("Shelf", k).Anchors(0.03f, 0.11f, 0.97f, 0.49f);
            var scroll = UIKit.ScrollList(shelf, 0, 2);
            ((RectTransform)scroll.parent.parent).Anchors(0, 0, 0.86f, 1);
            var tabs = UIKit.NewRect("ShelfTabs", shelf).Anchors(0.87f, 0.02f, 1f, 0.98f);
            UIKit.VList(tabs, 8, 0);
            foodShelfBtn = UIKit.Button(tabs, "Đồ ăn", () => { viewDrinks = false; gm.MarkDirty(); }, Pal.Orange, 20);
            foodShelfBtn.Size(h: 64);
            drinkShelfBtn = UIKit.Button(tabs, "Đồ uống", () => { viewDrinks = true; gm.MarkDirty(); }, Pal.Teal, 20);
            drinkShelfBtn.Size(h: 64);
            var shelfLabelColor = new Color(1f, 0.93f, 0.8f);
            shelfTopLabel = UIKit.Label(scroll, "", 18, shelfLabelColor, TextAnchor.MiddleLeft);
            shelfTopLabel.Size(h: 26);
            ingGridTop = UIKit.NewRect("GridTop", scroll);
            UIKit.Grid(ingGridTop, new Vector2(128, 140), new Vector2(5, 5), 2);
            shelfBottomLabel = UIKit.Label(scroll, "", 18, shelfLabelColor, TextAnchor.MiddleLeft);
            shelfBottomLabel.Size(h: 26);
            ingGridBottom = UIKit.NewRect("GridBottom", scroll);
            UIKit.Grid(ingGridBottom, new Vector2(128, 140), new Vector2(5, 5), 2);
            emptyShelfText = UIKit.Label(shelf, "", 26, new Color(1f, 0.93f, 0.8f), TextAnchor.MiddleCenter);
            emptyShelfText.rectTransform.Fill(20);
            emptyShelfText.gameObject.AddComponent<Shadow>().effectColor = new Color(0.2f, 0.1f, 0.05f, 0.8f);

            var bottom = UIKit.NewRect("Buttons", k).Anchors(0.03f, 0.018f, 0.97f, 0.1f);
            var bh = UIKit.HList(bottom, 10, 0);
            bh.childForceExpandWidth = true;
            quickBtn = UIKit.Button(bottom, "Ghép nhanh", () => { gm.Kitchen.QuickFill(); gm.TutorialSignal("quick"); }, new Color(0.3f, 0.55f, 0.3f), 28);
            clearPotBtn = UIKit.Button(bottom, "Đổ nồi", () => gm.Kitchen.ClearPot(), new Color(0.62f, 0.6f, 0.56f), 28);
            hintBtn = UIKit.Button(bottom, "Công thức", () => { S.showRecipeHint = !S.showRecipeHint; gm.MarkDirty(); gm.TutorialSignal("hint"); }, new Color(0.62f, 0.6f, 0.56f), 28);
            cookBtn = UIKit.Button(bottom, "CHẾ BIẾN", () => gm.Kitchen.Cook(), new Color(0.82f, 0.18f, 0.14f), 30);
        }

        public void ShowTitle()
        {
            CloseModal(false);
            gameRoot.gameObject.SetActive(false);
            titleLayer.gameObject.SetActive(true);
            UIKit.Clear(titleLayer);
            bobbers.RemoveAll(b => b.rt == null || b.rt.IsChildOf(titleLayer));

            var bg = UIKit.Box(titleLayer, new Color(0.58f, 0.72f, 0.85f), false, "TitleBg");
            bg.rectTransform.Fill();
            bg.gameObject.AddComponent<RectMask2D>();
            var sky = UIKit.NewRect("Sky", bg.transform).Anchors(0, 0.3f, 1, 1);
            Art.Fit(sky, Art.Get("scene/city"), true, "City");
            Art.Tiled(bg.transform, Art.Get("scene/floor"), "Floor").rectTransform.Anchors(0, 0, 1, 0.33f);
            var fbox = UIKit.NewRect("FacadeBox", bg.transform).Anchors(0.05f, 0.27f, 0.95f, 0.98f);
            var facade = Art.Fit(fbox, Art.Get("scene/facade"), false, "Facade");
            var sign = UIKit.Label(facade.transform, GameManager.ShopName, 80, Yellow, TextAnchor.MiddleCenter);
            sign.rectTransform.Anchors(0.12f, 0.65f, 0.88f, 0.785f);
            sign.Fit(80, 24);
            var so = sign.gameObject.AddComponent<Shadow>();
            so.effectColor = new Color(0.3f, 0.05f, 0.02f);
            so.effectDistance = new Vector2(4, -4);

            var col = UIKit.NewRect("Buttons", titleLayer).Anchors(0.33f, 0.08f, 0.67f, 0.22f);
            var h = UIKit.HList(col, 14, 0);
            h.childForceExpandWidth = true;
            if (GameManager.HasSave)
            {
                UIKit.Button(col, "CHƠI TIẾP", gm.ContinueGame, Pal.Red, 40);
                UIKit.Button(col, "CHƠI MỚI", () => ShowConfirm("Chơi mới?", "Dữ liệu đang lưu sẽ bị ghi đè.", "Chơi mới", gm.NewGame), Pal.Wood, 34);
            }
            else
            {
                UIKit.Button(col, "BẮT ĐẦU", gm.NewGame, Pal.Red, 44);
            }
            var backup = UIKit.Button(titleLayer, "SAO LƯU / KHÔI PHỤC DỮ LIỆU", OpenBackup, Pal.Teal, 26);
            ((RectTransform)backup.transform).Anchors(0.36f, 0.01f, 0.64f, 0.07f);
            if (!GameManager.HasSave && !autoRestoreAsked)
            {
                autoRestoreAsked = true;
                string found = BackupFile.FindBest(out var at);
                string info = found != null ? GameManager.BackupSummary(found) : null;
                if (info != null)
                    ShowConfirm("Tìm thấy dữ liệu cũ!", $"Trong máy có file sao lưu:\n{at}\n\n{info}\n\nKhôi phục để chơi tiếp?", "Khôi phục", () =>
                    {
                        if (gm.ImportBackupCode(found, out var err)) { Sfx.Play("fanfare"); gm.ContinueGame(); gm.Toast("Khôi phục thành công!"); }
                        else ShowMessage("KHÔNG KHÔI PHỤC ĐƯỢC", err, ShowTitle);
                    }, ShowTitle);
            }
        }

        bool autoRestoreAsked;

        public void ShowGame()
        {
            titleLayer.gameObject.SetActive(false);
            gameRoot.gameObject.SetActive(true);
            Refresh();
        }

        void Refresh()
        {
            refreshPending = false;
            if (gm.Phase == GamePhase.Title) return;
            RefreshHud();
            RefreshScene();
            UpdateArrows();
            RefreshKitchen();
            RefreshModal();
        }

        void RefreshHud()
        {
            var ch = gm.Chapter;
            dayText.Set($"Ngày {S.day}");
            int full = Mathf.RoundToInt(S.reputation);
            for (int i = 0; i < 5; i++) stars[i].sprite = Art.Get(i < full ? "ui/star_full" : "ui/star_empty");
            chapterText.Set(S.finished ? "Kinh doanh tự do" : $"Chương {ch.number}\n<size=18>ngày {S.ChapterDay}/{ch.days}</size>");
            cashText.text = Finance.Money(S.cash);
            cashText.color = S.cash < 0 ? new Color(1f, 0.45f, 0.4f) : Yellow;
            int unpaid = TaxSystem.UnpaidCount(S);
            taxBtn.Caption().text = unpaid > 0 ? $"Thuế ({unpaid})" : "Thuế";
            ((Image)taxBtn.targetGraphic).color = TaxSystem.HasOverdue(S) ? Pal.Red : unpaid > 0 ? Pal.Orange : Pal.Green;

            goalText.Set(ReviewStripText());
            closeBtn.gameObject.SetActive(gm.Phase == GamePhase.Open || gm.Phase == GamePhase.Closing);

            RefreshChat();
        }

        void RefreshScene()
        {
            bool prep = gm.Phase == GamePhase.Prep;
            prepOverlay.gameObject.SetActive(prep && !gm.TutorialHidesPrep);
            if (prep) UpdatePrepText();

            for (int t = 0; t < slots.Length; t++)
            {
                var slot = slots[t];
                bool exists = S.HasTable(t);
                var g = exists ? gm.Customers.AtTable(t) : null;
                if (g != null && !g.Arrived) g = null;
                string key = exists ? SlotKey(g) : "ghost";
                if (key == slotKeys[t]) continue;
                slotKeys[t] = key;

                bobbers.RemoveAll(b => b.rt == null || b.rt.IsChildOf(slot));
                foreach (var id in patienceBars.Where(kv => kv.Value == null || kv.Value.transform.IsChildOf(slot)).Select(kv => kv.Key).ToList())
                    patienceBars.Remove(id);
                foreach (var id in orderCells.Where(kv => kv.Value == null || kv.Value.IsChildOf(slot)).Select(kv => kv.Key).ToList())
                    orderCells.Remove(id);
                foreach (var id in serveBtns.Where(kv => kv.Value == null || kv.Value.IsChildOf(slot)).Select(kv => kv.Key).ToList())
                    serveBtns.Remove(id);
                foreach (var id in eatBars.Where(kv => kv.Value == null || kv.Value.transform.IsChildOf(slot)).Select(kv => kv.Key).ToList())
                    eatBars.Remove(id);
                UIKit.Clear(slot);
                slot.gameObject.SetActive(true);
                if (exists) BuildTable(slot, t, g);
                else BuildGhostTable(slot, t);
            }
            BuildWalkers();
        }

        void BuildGhostTable(RectTransform slot, int t)
        {
            var table = Art.Pixel(slot, Art.Get(t % 2 == 0 ? "scene/table_blue" : "scene/table_red"), Art.Px, "GhostTable");
            table.color = new Color(1, 1, 1, 0.28f);
            var trt = table.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0);
            trt.pivot = new Vector2(0.5f, 0);
            trt.anchoredPosition = Vector2.zero;
            var u = GameManager.Upgrades.First(x => x.id == "table");
            var btn = UIKit.Button(slot, $"+ Thêm bàn\n<size=16>{Finance.Short(u.cost)}</size>",
                () => ShowConfirm("Thêm bàn?", $"Mua bàn số {t + 1} ({CustomerSystem.AreaNames[CustomerSystem.AreaOf(t)]}) với giá {Finance.Money(u.cost)}?\nThêm bàn = thêm chỗ cho khách, nhưng bếp phải nấu kịp!", "Mua",
                    () => gm.BuyTable(t)), Pal.Green, 20);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.sizeDelta = new Vector2(150, 64);
            brt.anchoredPosition = new Vector2(0, 26);
        }

        string SlotKey(CustomerGroup g)
        {
            if (g == null) return "empty";
            var w = gm.Customers.WaiterFor(g.id);
            var sb = new System.Text.StringBuilder();
            sb.Append(g.id).Append('|').Append(g.serving).Append('|').Append(g.eating).Append('|').Append(w == null ? "" : w.phase.ToString())
              .Append('|').Append(gm.Customers.ClaimedBy(g)?.name ?? "")
              .Append('|').Append(gm.Kitchen.targetGroup == g.id ? gm.Kitchen.targetItem : -1);
            foreach (var it in g.items) sb.Append('|').Append((int)it.status);
            return sb.ToString();
        }

        void UpdatePrepText()
        {
            bool closed = S.closedDay == S.day;
            if (S.day == 1 && !closed) { prepText.Set(OpeningDayText()); openBtn.Caption().text = "MỞ CỬA BÁN HÀNG"; return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#A8201C>BUỔI SÁNG — NGÀY {S.day}</color>");
            sb.AppendLine(closed ? "Hôm nay quán không được mở cửa." : "Kiểm tra kho, ĐẶT HÀNG trước, nộp thuế / trả nợ rồi mở cửa lúc 9:00.");
            sb.AppendLine();
            if (gm.MorningNotes.Count == 0) sb.AppendLine("• Không có gì đặc biệt.");
            foreach (var n in gm.MorningNotes.Take(12)) sb.AppendLine("• " + n);
            if (S.deliveries.Count > 0)
            {
                var parts = S.deliveries.OrderBy(d => d.arriveTimer).Take(6)
                    .Select(d => $"{GameDB.GetIng(d.ing).name} x{d.qty} ({Mathf.CeilToInt(Mathf.Max(0, d.arriveTimer))}s)");
                sb.AppendLine($"<color=#1E5AA8>Hàng đang về: {string.Join(", ", parts)}{(S.deliveries.Count > 6 ? "..." : "")}</color>");
            }
            var lowStock = GameDB.UnlockedIngredients(S.chapter)
                .Where(d => Inventory.Count(S, d.id) + gm.Market.PendingQty(d.id) < 5).Select(d => d.name).ToList();
            if (lowStock.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"<color=#C62828>Sắp hết / hết: {string.Join(", ", lowStock.Take(10))}{(lowStock.Count > 10 ? "..." : "")}</color>");
            }
            prepText.Set(sb.ToString());
            openBtn.Caption().text = closed ? "QUA NGÀY (quán phải đóng cửa)" : "MỞ CỬA BÁN HÀNG";
        }

        string OpeningDayText()
        {
            var recipes = GameDB.UnlockedRecipes(S.chapter).Where(r => !S.disabledRecipes.Contains(r.id)).ToList();
            var foods = recipes.Where(r => !r.IsDrink).Select(r => r.name).ToList();
            int drinks = recipes.Count(r => r.IsDrink);
            bool restart = S.prisonCount > 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(restart ? "<color=#A8201C>NGÀY ĐẦU LÀM LẠI</color>" : "<color=#A8201C>NGÀY KHAI TRƯƠNG</color>");
            sb.AppendLine(restart
                ? "<i>Con ngõ cũ trên phố Thái Hà, chiếc biển hiệu mới tinh. Lần này làm ăn cho đàng hoàng.</i>"
                : "<i>Ngõ nhỏ trên phố Thái Hà, Đống Đa. Biển hiệu treo từ tối qua vẫn còn thơm mùi sơn, nồi nước dùng đầu tiên đang sôi lăn tăn.</i>");
            sb.AppendLine();
            sb.AppendLine($"• <b>Quán nhỏ:</b> {S.tables} bàn, {S.stoves} bếp nấu mì, {S.barTiers.Count} máy pha nước. Một mình Ánh vừa nấu vừa bưng.");
            sb.AppendLine($"• <b>Thực đơn:</b> {string.Join(", ", foods)} — và {drinks} loại nước giải cay.");
            sb.AppendLine("• <b>Giờ bán:</b> 9:00 – 23:00. Khách đông nhất lúc trưa (11h–13h) và chiều tối (17h30–19h30), đầu giờ chiều thì vắng.");
            sb.AppendLine("• <b>Khách Hà Nội</b> ăn cay từ cấp 0 tới cấp 7: nêm đúng độ cay khách gọi, đừng để ai chờ quá lâu.");
            sb.AppendLine(restart
                ? $"• <b>Vốn liếng:</b> khoản vay ngân hàng {Finance.Short(GameManager.PrisonLoan)}, nhớ trả đúng hạn."
                : $"• <b>Vốn liếng:</b> {Finance.Short(GameManager.StartCapital)} mẹ cho mượn, tiêu từng đồng cho khéo.");
            foreach (var n in gm.MorningNotes.Where(n => !n.StartsWith("Bắt đầu")).Take(4)) sb.AppendLine("• " + n);
            sb.AppendLine();
            sb.Append("<color=#1B7F2A><b>Chúc tiệm mỳ cay của Cún iêu buôn may bán đắt!</b></color>");
            return sb.ToString();
        }

        List<object> walkerSig = new List<object>();

        List<object> WalkerSignature()
        {
            var sig = new List<object>();
            foreach (var lv in gm.Customers.leavers) sig.Add(lv);
            foreach (var g in gm.Customers.groups) if (!g.Arrived) sig.Add(g);
            foreach (var w in gm.Customers.waiters)
            {
                if (w.phase == WaiterPhase.Idle) continue;
                sig.Add(w); sig.Add((int)w.phase); sig.Add(w.groupId); sig.Add(w.tray.Count);
            }
            foreach (var h in gm.Customers.helpers)
            {
                if (!h.Walking) continue;
                sig.Add(h); sig.Add(h.name); sig.Add(h.serveGroup); sig.Add(h.returning); sig.Add(h.Handing); sig.Add(h.tray.Count);
            }
            return sig;
        }

        void BuildWalkers()
        {
            var sig = WalkerSignature();
            if (sig.SequenceEqual(walkerSig)) return;
            walkerSig = sig;
            UIKit.Clear(walkersLayer);
            customerWalkers.Clear();
            waiterWalkers.Clear();
            leaverWalkers.Clear();
            helperWalkers.Clear();

            for (int k = 0; k < gm.Customers.leavers.Count; k++)
            {
                var lv = gm.Customers.leavers[k];
                var root = WalkerRoot("Ve" + k);
                float sc = GroupScale(lv.size);
                for (int m = 0; m < lv.size; m++)
                    Member(root, Art.Customer(lv.looks[m]), sc, new Vector2(MemberX(m, lv.size), 0));
                leaverWalkers.Add((root, lv));
                PlaceLeaver(root, lv);
            }

            foreach (var g in gm.Customers.groups.Where(x => !x.Arrived))
            {
                var root = WalkerRoot("Khach" + g.id);
                float sc = GroupScale(g.size);
                for (int m = 0; m < g.size; m++)
                    Member(root, Art.Customer(g.looks[m]), sc, new Vector2(MemberX(m, g.size), 0));
                customerWalkers.Add((root, g));
                PlaceCustomer(root, g);
            }

            foreach (var w in gm.Customers.waiters.Where(x => x.phase != WaiterPhase.Idle))
            {
                var root = WalkerRoot("PhucVu_" + w.id);
                Member(root, Art.Char(w.id), Art.Px, Vector2.zero);
                AddTray(root, w.tray);
                waiterWalkers.Add((root, w));
                PlaceWaiter(root, w);
            }

            foreach (var h in gm.Customers.helpers.Where(x => x.Walking))
            {
                var root = WalkerRoot("NhanVien_" + h.name);
                Member(root, Art.Char(GameManager.HelperSprite(h.name)), Art.Px, Vector2.zero);
                AddTray(root, h.tray);
                var tag = UIKit.Label(root, h.name, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                var trt = tag.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0);
                trt.sizeDelta = new Vector2(140, 26);
                trt.anchoredPosition = new Vector2(0, 150);
                tag.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
                helperWalkers.Add((root, h));
                PlaceHelper(root, h);
            }
        }

        readonly List<(RectTransform rt, CustomerSystem.Helper h)> helperWalkers = new List<(RectTransform, CustomerSystem.Helper)>();

        void AddTray(RectTransform root, List<RecipeDef> items)
        {
            if (items.Count == 0) return;
            var tray = UIKit.Box(root, new Color(0.62f, 0.4f, 0.22f), true, "Tray");
            tray.raycastTarget = false;
            var trt = tray.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0);
            trt.pivot = new Vector2(0.5f, 0);
            int n = Mathf.Min(4, items.Count);
            trt.sizeDelta = new Vector2(Mathf.Max(96, n * 44 + 16), 16);
            trt.anchoredPosition = new Vector2(0, 44);
            for (int i = 0; i < n; i++)
            {
                var food = Art.Pixel(root, Art.Food(items[i].id), 1.8f, "Food");
                var frt = food.rectTransform;
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0);
                frt.pivot = new Vector2(0.5f, 0);
                frt.anchoredPosition = new Vector2((i - (n - 1) / 2f) * 44, 54);
            }
        }

        void PlaceHelper(RectTransform root, CustomerSystem.Helper h)
        {
            int k = gm.Customers.CounterSlot(h);
            if (!h.Walking || k < 0)
            {
                root.localScale = Vector3.zero;
                return;
            }
            float p = h.Handing ? 1f : h.returning ? 1f - h.Progress : h.Progress;
            bool slot = k < frontHelperImgs.Length;
            float baseX = slot ? FrontHelperX[k] : CustomerSystem.TableX(h.table) < CustomerSystem.CounterX ? 0f : 1f;
            PlaceOnPath(root, slot ? frontHelperImgs[k] : null, baseX, h.table, p, h.returning, !h.Handing && !gm.IsPaused);
        }

        RectTransform WalkerRoot(string name)
        {
            var root = UIKit.NewRect(name, walkersLayer);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0);
            root.sizeDelta = new Vector2(220, 200);
            return root;
        }

        const float TableTop = 18 * 4f;
        const float SitDrop = 14f;

        static float GroupScale(int n) => n <= 1 ? 4f : n == 2 ? 3.6f : n == 3 ? 3.2f : 3f;
        static float GroupSpacing(int n) => n <= 1 ? 0f : n == 2 ? 66f : n == 3 ? 58f : 52f;
        static float MemberX(int i, int n) => (i - (n - 1) / 2f) * GroupSpacing(n);
        static float StandY(float scale) => TableTop - 8f * scale;

        static void Member(RectTransform root, Sprite sprite, float scale, Vector2 pos)
        {
            var img = Art.Pixel(root, sprite, scale, "Nguoi");
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = pos;
        }

        static Vector2 Step(bool moving) =>
            moving && ((int)(Time.unscaledTime * 8f)) % 2 == 0 ? new Vector2(0, 5) : Vector2.zero;

        void PlaceCustomer(RectTransform root, CustomerGroup g)
        {
            float edge = CustomerSystem.CustomerStartX(g.table, g.fromSide);
            float x = Mathf.Lerp(edge, CustomerSystem.TableX(g.table), g.WalkProgress);
            root.anchorMin = root.anchorMax = new Vector2(x, CustomerSystem.TableY(g.table));
            root.anchoredPosition = new Vector2(0, StandY(GroupScale(g.size))) + Step(!gm.IsPaused);
            root.localScale = new Vector3(g.fromSide > 0 ? -1 : 1, 1, 1);
        }

        void PlaceLeaver(RectTransform root, Leaver lv)
        {
            float edge = CustomerSystem.LeaverExitX(lv.table, lv.side);
            float x = Mathf.Lerp(CustomerSystem.TableX(lv.table), edge, lv.Progress);
            root.anchorMin = root.anchorMax = new Vector2(x, CustomerSystem.TableY(lv.table));
            root.anchoredPosition = new Vector2(0, StandY(GroupScale(lv.size))) + Step(!gm.IsPaused);
            root.localScale = new Vector3(lv.side > 0 ? 1 : -1, 1, 1);
        }

        static void SetStaffX(Image img, float x)
        {
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(x - StaffW / 2f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(x + StaffW / 2f, rt.anchorMax.y);
        }

        (float exitX, Vector2 exitPt, Vector2 downPt, Vector2 tablePt, float f0, float f1) WaiterPath(Waiter w) => StaffPath(StaffBaseX(w.id), w.table);

        (float exitX, Vector2 exitPt, Vector2 downPt, Vector2 tablePt, float f0, float f1) StaffPath(float baseX, int table)
        {
            int side = CustomerSystem.TableX(table) < CustomerSystem.CounterX ? -1 : 1;
            float exitX = side < 0 ? -StaffW * 0.6f : 1f + StaffW * 0.6f;
            var cr = counterFitRt.rect;
            var r = walkersLayer.rect;
            Vector2 exitPt = new Vector2(0.45f, 0.4f);
            if (r.width > 0 && r.height > 0 && cr.width > 0)
            {
                Vector3 local = new Vector3(cr.xMin + exitX * cr.width, cr.yMin + StaffFeetY * cr.height, 0);
                Vector2 wl = walkersLayer.InverseTransformPoint(counterFitRt.TransformPoint(local));
                exitPt = new Vector2((wl.x - r.xMin) / r.width, (wl.y - r.yMin) / r.height);
            }
            var tablePt = new Vector2(CustomerSystem.TableX(table) + 0.045f / CustomerSystem.WorldScreens * 2f * -side, CustomerSystem.TableY(table));
            var downPt = new Vector2(exitPt.x, tablePt.y);
            float l0 = Mathf.Abs(exitX - baseX) * Mathf.Max(1f, cr.width);
            float l1 = Mathf.Abs(exitPt.y - downPt.y) * Mathf.Max(1f, r.height);
            float l2 = Mathf.Abs(downPt.x - tablePt.x) * Mathf.Max(1f, r.width);
            float total = Mathf.Max(1f, l0 + l1 + l2);
            return (exitX, exitPt, downPt, tablePt, l0 / total, (l0 + l1) / total);
        }

        void PlaceWaiter(RectTransform root, Waiter w)
        {
            float p = w.phase == WaiterPhase.Going ? w.Progress : w.phase == WaiterPhase.Handing ? 1f : 1f - w.Progress;
            PlaceOnPath(root, w.id == "vy" ? vyImg : anhImg, StaffBaseX(w.id), w.table, p, w.phase == WaiterPhase.Returning,
                w.phase != WaiterPhase.Handing && !gm.IsPaused);
        }

        void PlaceOnPath(RectTransform root, Image img, float baseX, int table, float p, bool returning, bool moving)
        {
            var path = StaffPath(baseX, table);
            int side = path.exitX < 0 ? -1 : 1;

            if (p < path.f0)
            {
                if (img != null)
                {
                    SetStaffX(img, Mathf.Lerp(baseX, path.exitX, path.f0 <= 0 ? 1f : p / path.f0));
                    img.enabled = true;
                    img.rectTransform.localScale = new Vector3(returning ? -side : side, 1, 1);
                }
                root.localScale = Vector3.zero;
                return;
            }
            if (img != null) img.enabled = false;
            Vector2 pos = p < path.f1
                ? Vector2.Lerp(path.exitPt, path.downPt, (p - path.f0) / Mathf.Max(0.0001f, path.f1 - path.f0))
                : Vector2.Lerp(path.downPt, path.tablePt, (p - path.f1) / Mathf.Max(0.0001f, 1f - path.f1));
            root.anchorMin = root.anchorMax = pos;
            root.anchoredPosition = Step(moving);
            float dir = p < path.f1 ? side : Mathf.Sign(path.tablePt.x - path.downPt.x);
            if (dir == 0) dir = side;
            if (returning) dir = -dir;
            root.localScale = new Vector3(dir > 0 ? 1 : -1, 1, 1);
        }

        void UpdateStaffIdle()
        {
            foreach (var id in new[] { "anh", "vy" })
            {
                var img = id == "vy" ? vyImg : anhImg;
                if (img == null) continue;
                bool working = id == "anh" || S.ActiveStaff > 0;
                bool busy = gm.Customers.waiters.Any(w => w.id == id && w.phase != WaiterPhase.Idle);
                if (!working) { img.enabled = false; continue; }
                if (busy) continue;
                SetStaffX(img, StaffBaseX(id));
                img.rectTransform.localScale = Vector3.one;
                img.enabled = true;
            }
            int k = 0;
            foreach (var h in gm.Customers.helpers)
            {
                if (k >= frontHelperImgs.Length) break;
                var img = frontHelperImgs[k++];
                if (frontHelperNames[k - 1] != h.name)
                {
                    frontHelperNames[k - 1] = h.name;
                    img.sprite = Art.Char(GameManager.HelperSprite(h.name));
                }
                if (h.Walking) continue;
                SetStaffX(img, FrontHelperX[k - 1]);
                img.rectTransform.localScale = Vector3.one;
                img.enabled = true;
            }
            for (; k < frontHelperImgs.Length; k++)
                if (frontHelperImgs[k].enabled) frontHelperImgs[k].enabled = false;
        }

        void BuildTable(RectTransform slot, int t, CustomerGroup g)
        {
            if (g != null)
            {
                float sc = GroupScale(g.size);
                for (int m = 0; m < g.size; m++)
                {
                    var cust = Art.Pixel(slot, Art.Customer(g.looks[m]), sc, "Khach");
                    var crt = cust.rectTransform;
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0);
                    crt.pivot = new Vector2(0.5f, 0);
                    crt.anchoredPosition = new Vector2(MemberX(m, g.size), StandY(sc) - SitDrop);
                    Bob(crt, g.id * 0.37f + m * 0.9f);
                }
            }

            var table = Art.Pixel(slot, Art.Get(t % 2 == 0 ? "scene/table_blue" : "scene/table_red"), Art.Px, "Table");
            var trt = table.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0);
            trt.pivot = new Vector2(0.5f, 0);
            trt.anchoredPosition = Vector2.zero;
            var num = UIKit.Label(table.transform, $"{t + 1}", 22, Color.white, TextAnchor.UpperCenter);
            num.rectTransform.Fill();

            var waiter = g != null ? gm.Customers.WaiterFor(g.id) : null;
            if (g != null && (g.eating || waiter != null && waiter.phase == WaiterPhase.Handing))
            {
                int n = Mathf.Min(4, g.items.Count);
                for (int i = 0; i < n; i++)
                {
                    var food = Art.Pixel(slot, Art.Food(g.items[i].recipe.id), 1.8f, "OnTable");
                    var frt = food.rectTransform;
                    frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0);
                    frt.pivot = new Vector2(0.5f, 0);
                    frt.anchoredPosition = new Vector2((i - (n - 1) / 2f) * 38, 58);
                }
            }

            if (g != null) BuildBubble(slot, g);
        }

        void BuildBubble(RectTransform slot, CustomerGroup g)
        {
            var bubble = UIKit.Box(slot, Color.white, true, "Bubble");
            Art.Sliced(bubble, Art.Bubble);
            var brt = bubble.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0);
            brt.pivot = new Vector2(0.5f, 0);
            float sc = GroupScale(g.size);
            float headTop = StandY(sc) - SitDrop + 32f * sc;
            brt.anchoredPosition = new Vector2(0, headTop + 16f);
            var v = UIKit.VList(bubble, 3, 6, TextAnchor.UpperCenter);
            v.childForceExpandWidth = false;
            v.childAlignment = TextAnchor.UpperCenter;
            var fit = bubble.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var tail = Art.Pixel(slot, Art.Get("ui/bubble_tail"), Art.Px, "Tail");
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0);
            tail.rectTransform.pivot = new Vector2(0.5f, 1);
            tail.rectTransform.anchoredPosition = new Vector2(0, headTop + 20f);

            if (g.eating)
            {
                var eh = UIKit.NewRect("Eat", bubble.transform);
                eh.Size(h: 8, w: 120);
                UIKit.Bar(eh, new Color(0.85f, 0.85f, 0.85f), Pal.Orange, out var ef).Fill();
                UIKit.SetBar(ef, g.EatProgress);
                eatBars[g.id] = ef;
                return;
            }

            var grid = UIKit.NewRect("Items", bubble.transform);
            int cols = g.items.Count > 6 ? 4 : Mathf.Min(3, g.items.Count);
            float cell = cols >= 4 ? 46f : 50f;
            var gl = UIKit.Grid(grid, new Vector2(cell, cell), new Vector2(3, 3), 0);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = cols;
            foreach (var it in g.items) BuildOrderItem(grid, g, it);

            if (g.serving)
            {
                var w = gm.Customers.WaiterFor(g.id);
                string msg = w == null ? (gm.Customers.Covered(g) || gm.Customers.HelperServing(g) ? "Nhân viên mang ra..." : "Chờ người bưng...") : w.phase == WaiterPhase.Going ? "Đang mang ra..." : "Mời dùng món!";
                var lbl = UIKit.Label(bubble.transform, msg, 22, new Color(0.1f, 0.45f, 0.1f), TextAnchor.MiddleCenter);
                lbl.Size(h: 30, w: 160);
            }
            else if (g.AllReady)
            {
                var serve = UIKit.Button(bubble.transform, "GIAO MÓN", () => gm.Customers.Serve(g), Pal.Green, 22);
                serve.Size(h: 38, w: 160);
                serveBtns[g.id] = (RectTransform)serve.transform;
            }
            var barHolder = UIKit.NewRect("Patience", bubble.transform);
            barHolder.Size(h: 8, w: Mathf.Max(cols * (cell + 3) - 3, g.serving || g.AllReady ? 160f : 0f));
            UIKit.Bar(barHolder, new Color(0.85f, 0.85f, 0.85f), Pal.Green, out var fill).Fill();
            patienceBars[g.id] = fill;
            if (gm.Customers.ClaimedBy(g) != null)
            {
                var shade = UIKit.Box(bubble.transform, new Color(0.45f, 0.86f, 0.45f, 0.34f), true, "StaffShade");
                shade.raycastTarget = false;
                shade.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                shade.rectTransform.Fill();
            }
        }

        void BuildOrderItem(RectTransform grid, CustomerGroup g, OrderItem it)
        {
            bool selected = gm.Kitchen.targetGroup == g.id && gm.Kitchen.targetItem == it.uid;
            Color bg = it.status == ItemStatus.Ready ? new Color(0.75f, 0.95f, 0.7f)
                : it.status == ItemStatus.Cooking ? new Color(0.75f, 0.85f, 1f)
                : selected ? new Color(1f, 0.72f, 0.62f) : new Color(1f, 0.96f, 0.88f);
            var cell = UIKit.Box(grid, bg, true, "Item");
            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = cell;
            btn.onClick.AddListener(() => gm.Kitchen.Select(g, it));
            orderCells[it.uid] = (RectTransform)cell.transform;
            var icon = Art.Pixel(cell.transform, Art.Food(it.recipe.id), 2.2f, "Food");
            icon.rectTransform.Fill(3);
            if (it.status == ItemStatus.Cooking) icon.color = new Color(1, 1, 1, 0.55f);
            if (it.recipe.spicy)
            {
                var badge = UIKit.Box(cell.transform, new Color(0.85f, 0.15f, 0.1f), true, "Spice");
                badge.raycastTarget = false;
                var b = badge.rectTransform;
                b.anchorMin = b.anchorMax = new Vector2(1, 0);
                b.pivot = new Vector2(1, 0);
                b.sizeDelta = new Vector2(24, 22);
                var t = UIKit.Label(badge.transform, it.spice.ToString(), 20, Color.white, TextAnchor.MiddleCenter);
                t.rectTransform.Fill();
            }
            if (it.broth != null)
            {
                bool thai = it.broth == GameDB.BrothThai;
                var bb = UIKit.Box(cell.transform, thai ? new Color(0.95f, 0.6f, 0.15f) : new Color(0.8f, 0.2f, 0.1f), true, "Broth");
                bb.raycastTarget = false;
                var brt2 = bb.rectTransform;
                brt2.anchorMin = brt2.anchorMax = new Vector2(0, 1);
                brt2.pivot = new Vector2(0, 1);
                brt2.sizeDelta = new Vector2(30, 20);
                UIKit.Label(bb.transform, thai ? "TH" : "KC", 15, Color.white, TextAnchor.MiddleCenter).rectTransform.Fill();
            }
            if (it.HasOmits)
            {
                var minus = UIKit.Box(cell.transform, new Color(0.08f, 0.4f, 0.75f), true, "Omit");
                minus.raycastTarget = false;
                var mrt = minus.rectTransform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(1, 1);
                mrt.pivot = new Vector2(1, 1);
                mrt.sizeDelta = new Vector2(26, 22);
                UIKit.Label(minus.transform, "−" + it.omits.Count, 18, Color.white, TextAnchor.MiddleCenter).rectTransform.Fill();
            }
            if (it.HasExtras)
            {
                var plus = UIKit.Box(cell.transform, new Color(0.85f, 0.1f, 0.45f), true, "Extra");
                plus.raycastTarget = false;
                var prt = plus.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(1, 1);
                prt.pivot = new Vector2(1, 1);
                prt.sizeDelta = new Vector2(26, 22);
                UIKit.Label(plus.transform, "+" + it.extras.Count, 18, Color.white, TextAnchor.MiddleCenter).rectTransform.Fill();
            }
            if (it.status != ItemStatus.Waiting)
            {
                var tag = UIKit.Label(cell.transform, it.status == ItemStatus.Ready ? "XONG" : "nấu", 18,
                    it.status == ItemStatus.Ready ? new Color(0.1f, 0.45f, 0.1f) : Pal.Blue, TextAnchor.LowerLeft);
                tag.rectTransform.Fill(2);
            }
            if (selected) cell.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
        }

        string stationKey;
        bool stationCompact;

        void RefreshStations()
        {
            var list = gm.Kitchen.stations;
            int nStoves = list.Count(x => x.type == StationType.Stove);
            int nUnits = DrinkUnits(list).Count;
            int barCols = nUnits > 3 ? 2 : nUnits > 0 ? 1 : 0;
            int rows = Mathf.Max(1, Mathf.Max(Mathf.CeilToInt(nStoves / (float)StoveCols), barCols == 0 ? 0 : Mathf.CeilToInt(nUnits / (float)barCols)));
            int cols = StoveCols + barCols;

            var kb = new System.Text.StringBuilder().Append(rows).Append('x').Append(cols);
            foreach (var st in list) kb.Append('|').Append(st.Name).Append(gm.Kitchen.IsBroken(st) ? "!" : "");
            string key = kb.ToString();
            bool sameObjects = stationViews.Count == list.Count && stationViews.Select(v => v.st).SequenceEqual(list);
            if (key != stationKey || !sameObjects)
            {
                stationKey = key;
                BuildStations(rows, cols);
            }
            foreach (var v in stationViews) UpdateStationView(v);
        }

        static List<List<Station>> DrinkUnits(IEnumerable<Station> list)
        {
            var units = new List<List<Station>>();
            foreach (var st in list.Where(x => x.type == StationType.DrinkBar))
            {
                var last = units.Count > 0 ? units[units.Count - 1] : null;
                if (st.IsMachine && last != null && last[0].IsMachine && last[0].machine == st.machine) last.Add(st);
                else units.Add(new List<Station> { st });
            }
            return units;
        }

        void BuildStations(int rows, int cols)
        {
            UIKit.Clear(stationRow);
            stationViews.Clear();
            stationCompact = rows > 1;
            bool compact = stationCompact;
            foreach (var st in gm.Kitchen.stations.Where(x => x.type == StationType.Stove))
            {
                string sprite = GameManager.StoveTiers[Mathf.Clamp(st.tier, 0, GameManager.StoveTiers.Length - 1)].sprite;
                var stove = StationBox(st.index % StoveCols, st.index / StoveCols, rows, cols, st.Name, sprite, compact, out var box);
                var fire = UIKit.NewRect("Fire", stove.transform).Anchors(0.22f, 0.55f, 0.78f, 0.72f).gameObject.AddComponent<Image>();
                fire.sprite = Art.Get("scene/fire" + animFrame);
                fire.raycastTarget = false;
                fire.enabled = st.Busy;
                bool broken = gm.Kitchen.IsBroken(st);
                if (broken) stove.color = new Color(0.45f, 0.45f, 0.45f);
                AddStationView(box, st, fire, compact, broken, 0f, 1f);
            }
            var units = DrinkUnits(gm.Kitchen.stations);
            for (int u = 0; u < units.Count; u++)
            {
                var unit = units[u];
                StationBox(StoveCols + u / rows, u % rows, rows, cols, unit[0].Name, GameManager.BarTiers[Mathf.Clamp(unit[0].tier, 0, GameManager.BarTiers.Length - 1)].sprite, compact, out var box);
                for (int k = 0; k < unit.Count; k++)
                    AddStationView(box, unit[k], null, compact, false, k / (float)unit.Count, (k + 1) / (float)unit.Count);
            }
        }

        Image StationBox(int col, int row, int rows, int cols, string name, string sprite, bool compact, out Image box)
        {
            box = UIKit.Box(stationRow, new Color(0.35f, 0.22f, 0.13f, 0.55f), true, name);
            box.rectTransform.Anchors(col / (float)cols, 1f - (row + 1) / (float)rows, (col + 1) / (float)cols, 1f - row / (float)rows, StationGap / 2f);
            var holder = UIKit.NewRect("StoveBox", box.transform);
            if (compact) holder.Anchors(0.02f, 0.17f, 0.46f, 0.98f);
            else holder.Anchors(0.04f, 0.27f, 0.96f, 0.98f);
            return Art.Fit(holder, Art.Get(sprite) ?? Art.Get("scene/drinkbar") ?? Art.Get("scene/stove"), false, "Stove");
        }

        void AddStationView(Image box, Station st, Image fire, bool compact, bool broken, float s0, float s1)
        {
            var lbl = UIKit.Label(box.transform, "", 17, broken ? new Color(1f, 0.55f, 0.5f) : Color.white, compact ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            if (compact) lbl.rectTransform.Anchors(0.48f, Mathf.Lerp(0.98f, 0.17f, s1), 0.99f, Mathf.Lerp(0.98f, 0.17f, s0));
            else lbl.rectTransform.Anchors(Mathf.Lerp(0.02f, 0.98f, s0), 0.09f, Mathf.Lerp(0.02f, 0.98f, s1), 0.27f);
            lbl.Fit(17, 10);
            float bx0 = Mathf.Lerp(0.05f, 0.95f, s0) + (s0 > 0f ? 0.01f : 0f), bx1 = Mathf.Lerp(0.05f, 0.95f, s1) - (s1 < 1f ? 0.01f : 0f);
            UIKit.Bar(box.transform, new Color(0.2f, 0.15f, 0.1f), Pal.Orange, out var fill).Anchors(bx0, 0.03f, bx1, compact ? 0.14f : 0.085f);
            UIKit.SetBar(fill, st.Busy ? 1f - st.timeLeft / st.total : 0f);
            stationViews.Add((st, fill, fire, lbl));
        }

        void UpdateStationView((Station st, Image fill, Image fire, Text label) v)
        {
            var st = v.st;
            bool broken = gm.Kitchen.IsBroken(st);
            string text;
            if (stationCompact)
            {
                var tg = st.Busy ? gm.Customers.Find(st.groupId) : null;
                string status = broken ? "HỎNG" : st.Busy ? (tg != null ? $"Bàn {tg.table + 1}" : "khách bỏ") : "trống";
                text = st.slots > 1 ? $"{(st.slot == 0 ? "A" : "B")}: {status}" : st.Name + "\n" + status;
            }
            else text = st.Busy ? st.label : broken ? st.Name + " (HỎNG)" : st.Name + " (trống)";
            if (v.label.text != text) v.label.text = text;
            if (v.fire != null && v.fire.enabled != st.Busy)
            {
                v.fire.enabled = st.Busy;
                if (st.Busy) v.fire.sprite = Art.Get("scene/fire" + animFrame);
            }
        }

        string potKey;

        void RefreshKitchen()
        {
            RefreshStations();

            var it = gm.Kitchen.Target;
            var g = gm.Kitchen.TargetGroup;
            targetIcon.enabled = it != null;
            if (it != null && g != null)
            {
                targetIcon.sprite = Art.Food(it.recipe.id);
                targetText.Set($"<color=#A8201C>{it.Label}</color> — Bàn {g.table + 1} <size=18>(~{it.recipe.realMinutes} phút, {(it.recipe.IsDrink ? "máy pha" : "bếp")})</size>");
                hintText.Set(S.showRecipeHint
                    ? "Công thức: " + GameDB.RecipeText(it.recipe, it.spice, it.extras, it.broth, it.omits)
                    : $"(Đang ẩn công thức — tự nhớ nhé! Đã nấu món này {S.GetMastery(it.recipe.id)} lần)");
            }
            else
            {
                targetText.Set(gm.Phase == GamePhase.Open || gm.Phase == GamePhase.Closing
                    ? "Chạm vào món trong bong bóng của khách để bắt đầu."
                    : "Bếp chưa nhóm lửa. Mở cửa để đón khách.");
                hintText.Set("Chạm thẻ nguyên liệu để cho vào nồi, rồi bấm CHẾ BIẾN (đồ uống: PHA CHẾ). Mì cay: số muỗng bột ớt = cấp độ cay (số đỏ trên món).");
            }

            string pk = string.Join(",", gm.Kitchen.pot.GroupBy(x => x).Select(x => x.Key + ":" + x.Count())) + (it != null ? "|t" : "|n");
            if (pk != potKey)
            {
                potKey = pk;
                UIKit.Clear(potRow);
                foreach (var grp in gm.Kitchen.pot.GroupBy(x => x))
                {
                    var id = grp.Key;
                    var chip = UIKit.Box(potRow, new Color(1f, 0.93f, 0.8f), true, "Chip");
                    ((RectTransform)chip.transform).sizeDelta = new Vector2(64, 64);
                    var b = chip.gameObject.AddComponent<Button>();
                    b.targetGraphic = chip;
                    b.onClick.AddListener(() => gm.Kitchen.RemoveFromPot(id));
                    Art.Pixel(chip.transform, Art.Ing(id), 2.4f, "Icon").rectTransform.Fill(5);
                    var n = UIKit.Label(chip.transform, "x" + grp.Count(), 20, new Color(0.65f, 0.1f, 0.05f), TextAnchor.LowerRight);
                    n.rectTransform.Fill(2);
                }
                if (gm.Kitchen.pot.Count == 0 && it != null)
                {
                    var empty = UIKit.Label(potRow, "Nồi trống... (chạm nguyên liệu bên dưới)", 20, Pal.Gray, TextAnchor.MiddleLeft);
                    empty.rectTransform.sizeDelta = new Vector2(500, 60);
                }
            }

            int tgtUid = it != null ? it.uid : -1;
            if (tgtUid != viewTarget)
            {
                viewTarget = tgtUid;
                if (it != null) viewDrinks = it.recipe.IsDrink;
            }
            bool drinkMode = viewDrinks;
            ((Image)foodShelfBtn.targetGraphic).color = drinkMode ? Pal.Gray : Pal.Orange;
            ((Image)drinkShelfBtn.targetGraphic).color = drinkMode ? Pal.Teal : Pal.Gray;
            var defs = GameDB.UnlockedIngredients(S.chapter)
                .Where(d => d.IsDrink == drinkMode && (Inventory.Count(S, d.id) > 0 || gm.Kitchen.pot.Contains(d.id) || gm.Market.PendingQty(d.id) > 0))
                .ToList();
            bool OnTop(IngredientDef d) => drinkMode ? GameDB.IsDrinkTopping(d.id) : !GameDB.Broths.Contains(d.id);
            var topDefs = defs.Where(OnTop).ToList();
            var bottomDefs = defs.Where(d => !OnTop(d)).ToList();
            string key = (drinkMode ? "d" : "f") + "|" + string.Join("|", topDefs.Select(d => d.id)) + "#" + string.Join("|", bottomDefs.Select(d => d.id));
            if (key != ingKey)
            {
                ingKey = key;
                UIKit.Clear(ingGridTop);
                UIKit.Clear(ingGridBottom);
                ingCards.Clear();
                ingViews.Clear();
                foreach (var def in topDefs) BuildIngredientCard(def, ingGridTop);
                foreach (var def in bottomDefs) BuildIngredientCard(def, ingGridBottom);
            }
            shelfTopLabel.Set(drinkMode ? "Topping" : "Nguyên liệu");
            shelfBottomLabel.Set(drinkMode ? "Nước / chất lỏng" : "Nước lẩu");
            shelfTopLabel.gameObject.SetActive(topDefs.Count > 0);
            ingGridTop.gameObject.SetActive(topDefs.Count > 0);
            shelfBottomLabel.gameObject.SetActive(bottomDefs.Count > 0);
            ingGridBottom.gameObject.SetActive(bottomDefs.Count > 0);
            lowFlashes.Clear();
            foreach (var v in ingViews) UpdateIngredientCard(v);
            emptyShelfText.gameObject.SetActive(defs.Count == 0);
            if (defs.Count == 0)
                emptyShelfText.Set(drinkMode
                    ? "Tủ chưa có nguyên liệu pha chế.\nVào NHẬP HÀNG > Nguyên liệu pha chế để đặt hàng."
                    : "Tủ chưa có nguyên liệu nào.\nVào NHẬP HÀNG để đặt hàng — hàng về tới đâu sẽ hiện ra ở đây.");

            bool hasTarget = it != null;
            quickBtn.interactable = gm.Kitchen.CanQuickFill(it);
            quickBtn.Caption().text = !hasTarget ? "Ghép nhanh"
                : !gm.Kitchen.Mastered(it) ? $"Ghép nhanh {Mathf.Min(S.GetMastery(it.recipe.id), KitchenSystem.MasteryForQuickFill)}/{KitchenSystem.MasteryForQuickFill}"
                : it.IsCustom ? "Món riêng" : "Ghép nhanh";
            hintBtn.Caption().text = S.showRecipeHint ? "Ẩn c.thức" : "Hiện c.thức";
            cookBtn.interactable = hasTarget && gm.Kitchen.pot.Count > 0;
            cookBtn.Caption().text = hasTarget && it.recipe.IsDrink ? "PHA CHẾ" : "CHẾ BIẾN";
        }

        static string ShortName(IngredientDef def)
        {
            int k = def.name.IndexOf('(');
            return k > 0 ? def.name.Substring(0, k).Trim() : def.name;
        }

        class IngCardView
        {
            public string id;
            public Image card, icon, flash;
            public Text count, warn;
        }

        readonly List<IngCardView> ingViews = new List<IngCardView>();

        void BuildIngredientCard(IngredientDef def, RectTransform grid)
        {
            var id = def.id;
            var card = UIKit.Box(grid, Color.white, true, id);
            Art.Sliced(card, Art.Card);
            ingCards[id] = card.rectTransform;
            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = card;
            b.onClick.AddListener(() => gm.Kitchen.AddToPot(id));

            var icon = Art.Pixel(card.transform, Art.Ing(id), 3f, "Icon");
            icon.rectTransform.Anchors(0.2f, 0.38f, 0.8f, 0.84f);
            var count = UIKit.Label(card.transform, "", 22, Pal.Text, TextAnchor.UpperRight);
            count.rectTransform.Anchors(0.42f, 0.72f, 0.86f, 0.88f);
            var name = UIKit.Label(card.transform, ShortName(def), 17, Pal.Text, TextAnchor.MiddleCenter);
            name.rectTransform.Anchors(0.14f, 0.12f, 0.86f, 0.38f);
            name.Fit(17, 8);
            name.lineSpacing = 0.8f;
            ingViews.Add(new IngCardView { id = id, card = card, icon = icon, count = count });
        }

        void UpdateIngredientCard(IngCardView v)
        {
            int left = Inventory.Count(S, v.id) - gm.Kitchen.pot.Count(p => p == v.id);
            bool coming = left <= 0 && gm.Market.PendingQty(v.id) > 0;
            v.card.color = left <= 0 ? new Color(0.72f, 0.7f, 0.68f) : Color.white;
            v.icon.color = left <= 0 ? new Color(1, 1, 1, 0.4f) : Color.white;
            string txt = coming ? "đang về" : left.ToString();
            if (v.count.text != txt) v.count.text = txt;
            v.count.color = left > 0 ? new Color(0.65f, 0.1f, 0.05f) : coming ? Pal.Blue : Pal.Gray;
            v.count.Fit(coming ? 15 : 22, 9);

            bool low = gm.IsLowStock(v.id);
            if (low && v.flash == null)
            {
                v.flash = UIKit.Box(v.card.transform, new Color(1f, 0.1f, 0.05f, 0f), true, "LowFlash");
                v.flash.raycastTarget = false;
                v.flash.rectTransform.Fill(2);
                v.warn = UIKit.Label(v.flash.transform, "SẮP HẾT", 14, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
                v.warn.rectTransform.Anchors(0.06f, 0.86f, 0.94f, 0.99f);
                v.warn.Fit(14, 8);
            }
            else if (!low && v.flash != null)
            {
                Destroy(v.flash.gameObject);
                v.flash = null;
                v.warn = null;
            }
            if (v.flash != null) lowFlashes.Add((v.flash, v.warn));
        }


        readonly List<(Image box, Text label)> lowFlashes = new List<(Image, Text)>();

        void UpdateLowFlashes()
        {
            float a = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            for (int i = lowFlashes.Count - 1; i >= 0; i--)
            {
                var (box, label) = lowFlashes[i];
                if (box == null) { lowFlashes.RemoveAt(i); continue; }
                box.color = new Color(1f, 0.1f, 0.05f, 0.15f + 0.4f * a);
                label.color = new Color(1f, 1f, 1f, 0.4f + 0.6f * a);
            }
        }

        static readonly Color Clear0 = new Color(1f, 0.6f, 0.3f, 0f);
        static readonly Color Sunset = new Color(1f, 0.5f, 0.2f, 0.22f);
        static readonly Color Dusk = new Color(0.35f, 0.2f, 0.45f, 0.38f);
        static readonly Color Night = new Color(0.04f, 0.06f, 0.22f, 0.55f);

        static Color SkyTint(float clockMin)
        {
            float h = clockMin / 60f;
            if (h < 16f) return Clear0;
            if (h < 18f) return Color.Lerp(Clear0, Sunset, (h - 16f) / 2f);
            if (h < 19f) return Color.Lerp(Sunset, Dusk, h - 18f);
            if (h < 20f) return Color.Lerp(Dusk, Night, h - 19f);
            return Night;
        }

        void UpdateDaylight()
        {
            if (nightWorld == null) return;
            Color c = gm.Phase == GamePhase.Prep ? Clear0
                : gm.Phase == GamePhase.DayEnd ? Night
                : SkyTint(gm.ClockMinutes);
            nightWorld.color = c;
            nightSky.color = new Color(c.r * 0.6f, c.g * 0.6f, c.b, Mathf.Min(0.7f, c.a * 1.25f));
        }

        void Update()
        {
            animTimer += Time.unscaledDeltaTime;
            if (animTimer > 0.22f)
            {
                animTimer = 0;
                animFrame ^= 1;
                if (steamImg != null) steamImg.sprite = Art.Get("scene/steam" + animFrame);
                foreach (var v in stationViews)
                    if (v.fire != null && v.fire.enabled) v.fire.sprite = Art.Get("scene/fire" + animFrame);
            }
            float tt = Time.unscaledTime;
            for (int i = bobbers.Count - 1; i >= 0; i--)
            {
                var b = bobbers[i];
                if (b.rt == null) { bobbers.RemoveAt(i); continue; }
                b.rt.anchoredPosition = b.basePos + new Vector2(0, Mathf.Sin(tt * 3f + b.phase) > 0.6f ? 4f : 0f);
            }

            if (gm == null || gm.Phase == GamePhase.Title) { AgeToasts(); return; }
            if (scrollTarget >= 0f && sceneScroll != null)
            {
                float p = Mathf.MoveTowards(sceneScroll.horizontalNormalizedPosition, scrollTarget, Time.unscaledDeltaTime * 2.5f);
                sceneScroll.velocity = Vector2.zero;
                sceneScroll.horizontalNormalizedPosition = p;
                if (Mathf.Approximately(p, scrollTarget)) scrollTarget = -1f;
            }

            UpdateLowFlashes();
            foreach (var (rt, g) in customerWalkers)
                if (rt != null) PlaceCustomer(rt, g);
            UpdateStaffIdle();
            foreach (var (rt, h) in helperWalkers)
                if (rt != null) PlaceHelper(rt, h);
            foreach (var (rt, w) in waiterWalkers)
                if (rt != null) PlaceWaiter(rt, w);
            foreach (var (rt, l) in leaverWalkers)
                if (rt != null) PlaceLeaver(rt, l);
            foreach (var g in gm.Customers.groups)
                if (g.eating && eatBars.TryGetValue(g.id, out var ebar) && ebar != null) UIKit.SetBar(ebar, g.EatProgress);

            switch (gm.Phase)
            {
                case GamePhase.Prep: clockText.text = "Sáng"; break;
                case GamePhase.DayEnd: clockText.text = "Tối"; break;
                default:
                    int m = Mathf.FloorToInt(gm.ClockMinutes);
                    clockText.text = $"{m / 60:00}:{m % 60:00}";
                    break;
            }
            UpdateDaylight();

            if (gm.Phase == GamePhase.Prep && S.deliveries.Count > 0)
            {
                prepTimer -= Time.unscaledDeltaTime;
                if (prepTimer <= 0) { prepTimer = 0.5f; UpdatePrepText(); }
            }
            if (gm.IsPaused && (gm.Phase == GamePhase.Open || gm.Phase == GamePhase.Closing))
                clockText.Set(clockText.text + "<size=18> dừng</size>");

            foreach (var g in gm.Customers.groups)
            {
                if (!patienceBars.TryGetValue(g.id, out var fill) || fill == null) continue;
                float r = g.PatienceRatio;
                UIKit.SetBar(fill, r);
                fill.color = r > 0.5f ? Pal.Green : r > 0.25f ? Pal.Orange : Pal.Red;
            }
            foreach (var v in stationViews)
            {
                if (v.fill == null) continue;
                UIKit.SetBar(v.fill, v.st.Busy ? 1f - v.st.timeLeft / v.st.total : 0f);
            }
            AgeToasts();
        }

        void ShowToast(string msg)
        {
            if (gm != null && (gm.DialogueActive || gm.Phase != GamePhase.Title && modal == null)) return;
            foreach (var old in toasts) if (old.go != null) Destroy(old.go);
            toasts.Clear();
            var box = DarkBox(toastLayer, "Toast", 0.92f);
            box.raycastTarget = false;
            box.rectTransform.Fill();
            var t = UIKit.Label(box.transform, msg, 20, Color.white, TextAnchor.MiddleCenter);
            t.rectTransform.Fill(3);
            t.Fit(20, 11);
            toasts.Add((box.gameObject, 3f));
        }

        void AgeToasts()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var (go, t) = toasts[i];
                t -= Time.unscaledDeltaTime;
                if (go == null) { toasts.RemoveAt(i); continue; }
                if (t <= 0) { Destroy(go); toasts.RemoveAt(i); continue; }
                toasts[i] = (go, t);
                if (t < 0.5f)
                {
                    foreach (var g in go.GetComponentsInChildren<Graphic>())
                    {
                        var c = g.color;
                        c.a = Mathf.Min(c.a, t * 2f);
                        g.color = c;
                    }
                }
            }
        }
    }
}
