using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MiCay.UI
{
    public partial class MainUI
    {
        const float FingerScale = 5f;
        RectTransform pointerLayer;
        Image finger;
        TutorialBlocker blocker;
        readonly Vector3[] corners = new Vector3[4];
        static Sprite fingerSprite;

        void BuildPointer()
        {
            pointerLayer = UIKit.NewRect("Pointer", safe).Fill();
            var shield = UIKit.Box(pointerLayer, new Color(0, 0, 0, 0), false, "TutorialShield");
            shield.rectTransform.Fill();
            blocker = shield.gameObject.AddComponent<TutorialBlocker>();
            shield.gameObject.SetActive(false);

            var box = UIKit.Box(pointerLayer, new Color(1f, 0.95f, 0.6f, 0.97f), true, "TutorialBox");
            box.raycastTarget = false;
            tutorialBox = box.rectTransform;
            var o = box.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.75f, 0.2f, 0.1f);
            o.effectDistance = new Vector2(3, -3);
            tutorialText = UIKit.Label(box.transform, "", 22, Pal.Text, TextAnchor.MiddleLeft);
            tutorialText.rectTransform.Anchors(0.015f, 0.04f, 0.985f, 0.96f);
            tutorialText.Fit(22, 12);
            tutorialBox.gameObject.SetActive(false);
            finger = UIKit.NewRect("Finger", pointerLayer).gameObject.AddComponent<Image>();
            finger.sprite = FingerSprite();
            finger.raycastTarget = false;
            var rt = finger.rectTransform;
            rt.sizeDelta = new Vector2(12 * FingerScale, 16 * FingerScale);
            rt.pivot = new Vector2(4.5f / 12f, 1f);
            var sh = finger.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.45f);
            sh.effectDistance = new Vector2(4, -4);
            finger.enabled = false;
        }

        static Sprite FingerSprite()
        {
            if (fingerSprite != null) return fingerSprite;
            string[] rows =
            {
                "....OO......",
                "...OWWO.....",
                "...OWWO.....",
                "...OWWO.....",
                "...OWWOOO...",
                "...OWWOWWOO.",
                ".OOOWWOWWOWO",
                "OWWOWWWWWOWO",
                "OWWWWWWWWWWO",
                "OWWWWWWWWWWO",
                ".OWWWWWWWWWO",
                ".OWWWWWWWWO.",
                "..OWWWWWWWO.",
                "..OWWWWWWO..",
                "...OWWWWWO..",
                "...OOOOOOO..",
            };
            var outline = new Color32(70, 35, 20, 255);
            var skin = new Color32(255, 222, 186, 255);
            var tex = new Texture2D(12, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[12 * 16];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 12; x++)
            {
                char c = rows[15 - y][x];
                px[y * 12 + x] = c == 'O' ? outline : c == 'W' ? skin : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            fingerSprite = Sprite.Create(tex, new Rect(0, 0, 12, 16), new Vector2(0.5f, 0.5f), 100f);
            return fingerSprite;
        }

        bool MessageOpen => modal != null && Alive(modalCloseBtn) == null;

        bool TutorialLocked => gm != null && gm.State != null && gm.TutorialLocking && !gm.DialogueActive && !MessageOpen
                               && gm.Phase != GamePhase.Title && gm.Phase != GamePhase.DayEnd;

        static RectTransform TabButton(RectTransform row, int index)
        {
            if (row == null) return null;
            var tabs = row.GetComponentsInChildren<Button>();
            return index < tabs.Length ? Alive(tabs[index]) : null;
        }

        static RectTransform Alive(Component c) => c != null && c.gameObject.activeInHierarchy ? (RectTransform)c.transform : null;

        RectTransform ModalExit() => Alive(modalCloseBtn) ?? Alive(modalOkBtn);

        RectTransform PointerTarget()
        {
            if (gm == null || gm.State == null || gm.DialogueActive) return null;
            if (gm.Phase == GamePhase.Title || gm.Phase == GamePhase.DayEnd) return null;
            if (MessageOpen) return null;
            if (!gm.TutorialActive) return null;
            bool modalOpen = modal != null;
            switch (gm.TutorialStepId)
            {
                case "market":
                case "market2":
                case "restock_market":
                    return modalOpen ? ModalExit() : Alive(marketBtn);
                case "buy":
                    if (!modalOpen) return Alive(marketBtn);
                    return MarketTarget() ?? ModalExit();
                case "restock_buy":
                    if (!modalOpen) return Alive(marketBtn);
                    return MarketTarget(gm.RestockIngredient) ?? ModalExit();
                case "stock":
                    if (!modalOpen) return Alive(marketBtn);
                    return TabButton(marketTabsRow, 2) ?? ModalExit();
                case "menu":
                    return modalOpen ? ModalExit() : Alive(menuBtn);
                case "price":
                    if (!modalOpen) return Alive(menuBtn);
                    return Alive(priceBtns[1]) ?? ModalExit();
                case "finance":
                case "tour_finance":
                    return modalOpen ? ModalExit() : Alive(financeBtn);
                case "ledger":
                    if (!modalOpen) return Alive(financeBtn);
                    return TabButton(financeTabsRow, 5) ?? ModalExit();
                case "debt_tab":
                    if (!modalOpen) return Alive(financeBtn);
                    return TabButton(financeTabsRow, 6) ?? ModalExit();
                case "bank_tab":
                    if (!modalOpen) return Alive(financeBtn);
                    return TabButton(financeTabsRow, 7) ?? ModalExit();
                case "tax":
                case "tax_open":
                    return modalOpen ? ModalExit() : Alive(taxBtn);
                case "tax_pay":
                    if (!modalOpen) return Alive(taxBtn);
                    return Alive(taxPayBtn) ?? ModalExit();
                case "hint_hide":
                case "hint_show":
                    return modalOpen ? ModalExit() : Alive(hintBtn);
                case "topping":
                {
                    if (modalOpen) return ModalExit();
                    var top = gm.ToppingOrder();
                    return top != null && orderCells.TryGetValue(top.uid, out var tc) && tc != null ? tc : null;
                }
                case "quick_pick":
                {
                    if (modalOpen) return ModalExit();
                    var q = gm.QuickOrder();
                    if (q == null) return null;
                    var qg = gm.Customers.groups.FirstOrDefault(x => x.items.Contains(q));
                    if (qg != null)
                    {
                        var (vmin, vmax) = VisibleRange();
                        float qx = CustomerSystem.TableX(qg.table);
                        if (qx < vmin) return Alive(leftArrow);
                        if (qx > vmax) return Alive(rightArrow);
                    }
                    return orderCells.TryGetValue(q.uid, out var qc) && qc != null ? qc : null;
                }
                case "quick":
                    return modalOpen ? ModalExit() : Alive(quickBtn);
                case "close_market":
                case "close_menu":
                case "close_finance":
                case "close_tax":
                case "close_service":
                    return modalOpen ? ModalExit() : null;
                case "area_right":
                    return modalOpen ? ModalExit() : Alive(rightArrow);
                case "area_left":
                    return modalOpen ? ModalExit() : Alive(leftArrow);
                case "open":
                    if (modalOpen) return ModalExit();
                    return gm.Phase == GamePhase.Prep ? Alive(openBtn) : null;
                case "order":
                case "cook":
                case "serve":
                    if (modalOpen)
                    {
                        var lacking = LackingIngredient();
                        return (lacking != null ? MarketTarget(lacking) : null) ?? ModalExit();
                    }
                    return KitchenTarget();
                case "service":
                    return modalOpen ? ModalExit() : Alive(serviceBtn);
                default:
                    return null;
            }
        }

        RectTransform MarketTarget() => MarketTarget(gm.TutorialNextIngredient);

        RectTransform MarketTarget(IngredientDef next)
        {
            if (next == null || marketTabsRow == null) return null;
            int wantTab = next.IsDrink ? 1 : 0;
            if (marketTab != wantTab)
            {
                var tabs = marketTabsRow.GetComponentsInChildren<Button>();
                return wantTab < tabs.Length ? Alive(tabs[wantTab]) : null;
            }
            return orderBtns.TryGetValue(next.id, out var b) ? Alive(b) : null;
        }

        IngredientDef LackingIngredient()
        {
            var it = gm.Kitchen.Target;
            if (it == null) return null;
            var need = it.Needs().Keys.ToList();
            if (it.recipe.spicy && it.spice > 0) need.Add(GameDB.Chili);
            var id = need.FirstOrDefault(x => Inventory.Count(S, x) + gm.Market.PendingQty(x) <= 0);
            return id == null ? null : GameDB.GetIng(id);
        }

        void ScrollIntoView(RectTransform target)
        {
            var sr = target.GetComponentInParent<ScrollRect>();
            if (sr == null || sr.content == null || sr.viewport == null || !sr.vertical) return;
            var view = new Vector3[4];
            sr.viewport.GetWorldCorners(view);
            target.GetWorldCorners(corners);
            if (corners[0].y >= view[0].y - 1 && corners[1].y <= view[1].y + 1) return;
            float contentH = sr.content.rect.height, viewH = sr.viewport.rect.height;
            if (contentH <= viewH) return;
            Vector2 local = sr.content.InverseTransformPoint((corners[0] + corners[1]) / 2f);
            float fromTop = -local.y;
            sr.velocity = Vector2.zero;
            sr.verticalNormalizedPosition = 1f - Mathf.Clamp01((fromTop - viewH / 2f) / (contentH - viewH));
        }

        RectTransform KitchenTarget()
        {
            foreach (var g in gm.Customers.groups)
                if (g.Arrived && g.AllReady && !g.serving && serveBtns.TryGetValue(g.id, out var sb) && sb != null)
                    return sb;

            var it = gm.Kitchen.Target;
            if (it == null)
            {
                foreach (var g in gm.Customers.groups.Where(x => x.Arrived && !x.serving && !x.eating).OrderBy(x => x.arrivedSeq))
                foreach (var item in g.items.Where(i => i.status == ItemStatus.Waiting))
                    if (gm.Customers.FirstWaitingBefore(g, item.recipe.Station) == null &&
                        orderCells.TryGetValue(item.uid, out var cell) && cell != null) return cell;
                return null;
            }

            var need = it.Needs();
            if (it.recipe.spicy && it.spice > 0) need[GameDB.Chili] = it.spice;
            var have = gm.Kitchen.pot.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());

            if (have.Any(h => !need.TryGetValue(h.Key, out int n) || h.Value > n)) return Alive(clearPotBtn);
            foreach (var kv in need)
            {
                have.TryGetValue(kv.Key, out int h);
                if (h >= kv.Value) continue;
                return ingCards.TryGetValue(kv.Key, out var card) && card != null && Inventory.Count(S, kv.Key) > h
                    ? card
                    : Alive(marketBtn);
            }
            return Alive(cookBtn);
        }

        void UpdatePointer()
        {
            if (finger == null) return;
            if (modal == null && gm != null && gm.TutorialStepId != null && gm.TutorialStepId.StartsWith("close_")) gm.TutorialSignal("close");
            var target = PointerTarget();
            UpdateTutorialBox(target);
            bool locked = TutorialLocked;
            blocker.hole = target != null && target.gameObject.activeInHierarchy ? target : null;
            if (blocker.gameObject.activeSelf != locked) blocker.gameObject.SetActive(locked);
            if (target == null || !target.gameObject.activeInHierarchy) { finger.enabled = false; return; }

            ScrollIntoView(target);
            target.GetWorldCorners(corners);
            Vector2 min = pointerLayer.InverseTransformPoint(corners[0]);
            Vector2 max = pointerLayer.InverseTransformPoint(corners[2]);
            Vector2 center = (min + max) / 2f;
            var r = pointerLayer.rect;
            bool fromAbove = center.y < r.yMin + r.height * 0.3f;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 16f;
            float dy = (max.y - min.y) * 0.25f + bob;

            var rt = finger.rectTransform;
            rt.localRotation = Quaternion.Euler(0, 0, fromAbove ? 180f : 0f);
            rt.localPosition = new Vector3(center.x, center.y + (fromAbove ? dy : -dy), 0);
            finger.enabled = true;
        }

        void UpdateTutorialBox(RectTransform target)
        {
            bool show = gm != null && gm.State != null && gm.TutorialActive && !gm.DialogueActive && !MessageOpen
                        && gm.Phase != GamePhase.Title && gm.Phase != GamePhase.DayEnd;
            bool inScene = show && modal == null, floating = show && modal != null;
            if (tutStrip.gameObject.activeSelf != inScene) tutStrip.gameObject.SetActive(inScene);
            if (tutorialBox.gameObject.activeSelf != floating) tutorialBox.gameObject.SetActive(floating);
            float bottom = ChatH + (inScene ? TutH : 0f) + 0.012f;
            if (!Mathf.Approximately(bottom, prepBottom))
            {
                prepBottom = bottom;
                prepOverlay.Anchors(0.08f, bottom, 0.92f, 0.86f);
            }
            if (inScene) tutStripText.Set(gm.TutorialText);
            if (!floating) return;
            tutorialText.Set(gm.TutorialText);

            const float h = 0.15f;
            float left = 0f, right = 0f;
            if (target != null && target.gameObject.activeInHierarchy)
            {
                target.GetWorldCorners(corners);
                var r = pointerLayer.rect;
                Vector2 a = pointerLayer.InverseTransformPoint(corners[0]);
                Vector2 b = pointerLayer.InverseTransformPoint(corners[2]);
                float nx0 = (a.x - r.xMin) / r.width, nx1 = (b.x - r.xMin) / r.width;
                float ny0 = (a.y - r.yMin) / r.height;
                if (ny0 < h + 0.13f)
                {
                    left = Mathf.Max(0f, Mathf.Min(nx1, 0.6f) - Mathf.Max(nx0, 0.005f));
                    right = Mathf.Max(0f, Mathf.Min(nx1, 0.995f) - Mathf.Max(nx0, 0.4f));
                }
            }
            if (right < left) tutorialBox.Anchors(0.4f, 0.045f, 0.995f, 0.045f + h);
            else tutorialBox.Anchors(0.005f, 0.045f, 0.6f, 0.045f + h);
        }
    }

    public class TutorialBlocker : MonoBehaviour, ICanvasRaycastFilter
    {
        public RectTransform hole;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
            hole == null || !RectTransformUtility.RectangleContainsScreenPoint(hole, screenPoint, eventCamera);
    }
}
