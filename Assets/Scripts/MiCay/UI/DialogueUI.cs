using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiCay.UI
{
    public class DialogueUI : MonoBehaviour
    {
        const float CharsPerSecond = 55f;

        RectTransform root;
        Image figure, portrait;
        Text nameText, roleText, bodyText;
        Image nameTag;
        readonly List<DialogueLine> queue = new List<DialogueLine>();
        int index;
        float shown;
        Action onDone;

        public void Build(Transform parent)
        {
            root = UIKit.NewRect("Dialogue", parent).Fill();
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0.4f);
            var btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Advance);

            var panel = UIKit.Box(root, new Color(0.16f, 0.11f, 0.09f, 0.97f), false, "Panel");
            Art.Sliced(panel, Art.Dark);
            if (Art.Dark != null) panel.color = Color.white;
            panel.rectTransform.Anchors(0.02f, 0.03f, 0.98f, 0.32f);
            panel.raycastTarget = false;

            figure = Art.Pixel(root, null, 10f, "Figure");
            var frt = figure.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.03f, 0.03f);
            frt.pivot = new Vector2(0, 0);
            frt.sizeDelta = new Vector2(22 * 10, 32 * 10);

            var tag = UIKit.Box(panel.transform, new Color(0.36f, 0.22f, 0.13f), true, "NameTag");
            tag.rectTransform.Anchors(0.17f, 0.74f, 0.55f, 0.96f);
            tag.raycastTarget = false;
            nameTag = tag;
            portrait = Art.Pixel(tag.transform, null, 2f, "Portrait");
            portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = new Vector2(0, 0.5f);
            portrait.rectTransform.pivot = new Vector2(0, 0.5f);
            portrait.rectTransform.anchoredPosition = new Vector2(10, 0);
            portrait.rectTransform.sizeDelta = new Vector2(44, 40);
            nameText = UIKit.Label(tag.transform, "", 32, new Color(1f, 0.85f, 0.35f));
            nameText.rectTransform.Anchors(0.17f, 0, 1, 1);

            roleText = UIKit.Label(panel.transform, "", 22, new Color(1, 1, 1, 0.6f));
            roleText.rectTransform.Anchors(0.57f, 0.76f, 0.98f, 0.95f);

            bodyText = UIKit.Label(panel.transform, "", 34, Color.white, TextAnchor.UpperLeft);
            bodyText.rectTransform.Anchors(0.17f, 0.12f, 0.98f, 0.7f);
            bodyText.Fit(34, 18);

            var hint = UIKit.Label(panel.transform, "Chạm để tiếp tục >>", 20, new Color(1, 1, 1, 0.55f), TextAnchor.LowerRight);
            hint.rectTransform.Anchors(0.6f, 0.02f, 0.985f, 0.14f);

            var skip = UIKit.Button(root, "Bỏ qua", SkipAll, Pal.Gray, 26);
            ((RectTransform)skip.transform).Anchors(0.86f, 0.34f, 0.97f, 0.41f);

            root.gameObject.SetActive(false);
        }

        public void Play(IList<DialogueLine> lines, Action done)
        {
            queue.Clear();
            queue.AddRange(lines);
            index = 0;
            onDone = done;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            ShowLine();
        }

        void ShowLine()
        {
            var line = queue[index];
            var ch = StoryDB.Char(line.who);
            bool narrator = string.IsNullOrEmpty(ch.name);
            figure.enabled = !narrator;
            portrait.enabled = !narrator;
            if (!narrator)
            {
                figure.sprite = Art.Char(ch.sprite);
                portrait.sprite = Art.Portrait(ch.sprite);
            }
            nameTag.gameObject.SetActive(!narrator);
            nameText.text = narrator ? "" : ch.name;
            roleText.text = narrator ? "" : ch.role;
            bodyText.fontStyle = narrator ? FontStyle.Italic : FontStyle.Normal;
            bodyText.color = narrator ? new Color(0.85f, 0.9f, 1f) : Color.white;
            shown = 0;
            bodyText.text = "";
        }

        void Update()
        {
            if (root == null || !root.gameObject.activeSelf || queue.Count == 0) return;
            var full = queue[index].text;
            if (shown < full.Length)
            {
                shown += Time.unscaledDeltaTime * CharsPerSecond;
                bodyText.text = full.Substring(0, Mathf.Min(full.Length, Mathf.FloorToInt(shown)));
            }
        }

        void Advance()
        {
            if (queue.Count == 0) return;
            var full = queue[index].text;
            if (shown < full.Length)
            {
                shown = full.Length;
                bodyText.text = full;
                return;
            }
            index++;
            Sfx.Play("blip");
            if (index >= queue.Count) Finish();
            else ShowLine();
        }

        void SkipAll() => Finish();

        void Finish()
        {
            queue.Clear();
            root.gameObject.SetActive(false);
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }
    }
}
