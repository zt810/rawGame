using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiCay.UI
{
    public static class Art
    {
        public const float Px = 4f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static Sprite Get(string path, int border = 0, float scale = Px)
        {
            string key = path + "#" + border + "#" + scale;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = Resources.Load<Texture2D>("Art/" + path);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f / scale, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                s.name = path;
            }
            cache[key] = s;
            return s;
        }

        public static Sprite Char(string id) => Get("chars/" + id) ?? Get("chars/khach_01");
        public static Sprite Portrait(string id) => Get("portraits/" + id) ?? Get("portraits/khach_01");
        public static string CustomerId(int look) => $"khach_{look % CustomerSystem.CustomerLooks + 1:00}";
        public static Sprite Customer(int look) => Char(CustomerId(look));
        public static Sprite Food(string recipeId) => Get("food/" + recipeId);
        public static Sprite Ing(string ingId) => Get("ing/" + ingId);

        public static Sprite Btn => Get("ui/btn", 4);
        public static Sprite BoxFrame => Get("ui/box", 4);
        public static Sprite Card => Get("ui/card", 5);
        public static Sprite Bubble => Get("ui/bubble", 4);
        public static Sprite Paper => Get("ui/paper", 6);
        public static Sprite Dark => Get("ui/dark", 3);
        public static Sprite WoodPanel => Get("ui/wood_panel", 8);
        public static Sprite WoodTile => Get("ui/wood_tile");

        public static Image Pixel(Transform parent, Sprite s, float scale = Px, string name = "Pixel")
        {
            var rt = UIKit.NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (s != null) rt.sizeDelta = new Vector2(s.rect.width * scale, s.rect.height * scale);
            return img;
        }

        public static Image Fit(Transform parent, Sprite s, bool envelope = false, string name = "Fit")
        {
            var rt = UIKit.NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            if (s != null)
            {
                var f = rt.gameObject.AddComponent<AspectRatioFitter>();
                f.aspectMode = envelope ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.FitInParent;
                f.aspectRatio = s.rect.width / s.rect.height;
            }
            return img;
        }

        public static Image Sliced(Image img, Sprite s)
        {
            if (s == null) return img;
            img.sprite = s;
            img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Tiled(Transform parent, Sprite s, string name = "Tiled")
        {
            var rt = UIKit.NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            if (s != null)
            {
                s.texture.wrapMode = TextureWrapMode.Repeat;
                img.sprite = s;
                img.type = Image.Type.Tiled;
            }
            return img;
        }
    }
}
