using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiCay
{
    public static class GameDB
    {
        public const string Chili = "ot_bot";
        public const string BrothKimchi = "nuoc_kimchi", BrothThai = "nuoc_lau_thai";
        public static readonly string[] Broths = { BrothKimchi, BrothThai };
        public static string BrothShort(string id) => id == BrothThai ? "lẩu Thái" : "kim chi";
        public const int MaxSpice = 7;

        static readonly HashSet<string> DrinkSolids = new HashSet<string>
        {
            "chanh", "tac", "da", "bot_sua", "tran_chau", "cam", "dao_ngam", "vai_ngam",
            "thach_dua", "pudding", "kem_cheese", "duong_den"
        };
        public static bool IsDrinkTopping(string id) => DrinkSolids.Contains(id);

        public static string AmountText(IngredientDef d, int portions)
        {
            var m = System.Text.RegularExpressions.Regex.Match(d.unit, @"(\d+(?:,\d+)?)\s*(g|ml)\b");
            if (m.Success)
            {
                double each = double.Parse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
                double total = each * portions;
                bool ml = m.Groups[2].Value == "ml";
                return total >= 1000
                    ? (total / 1000).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + (ml ? " lít" : "kg")
                    : total.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + (ml ? "ml" : "g");
            }
            return d.unit.Any(char.IsDigit) || d.unit.StartsWith("phần") ? null : $"{portions} {d.unit}";
        }

        public static readonly Dictionary<string, IngredientDef> Ing = new Dictionary<string, IngredientDef>();
        public static readonly List<IngredientDef> IngList = new List<IngredientDef>();
        public static readonly List<RecipeDef> Recipes = new List<RecipeDef>();
        public static readonly Dictionary<string, RecipeDef> RecipeById = new Dictionary<string, RecipeDef>();

#pragma warning disable 0649
        [Serializable] class IngredientJson
        {
            public string id, name, unit, category, icon, color, color2, toppingFor, retailUnit, packUnit;
            public int retailQty, retailPrice, pack, packPrice, shelfDays, topping, toppingPrice;
        }
        [Serializable] class IngredientFile { public IngredientJson[] ingredients; }

        [Serializable] class ItemJson { public string ing; public int qty = 1; }
        [Serializable] class RecipeJson
        {
            public string id, name, category, note, broth;
            public int price, realMinutes, chapter = 1;
            public float cookSeconds;
            public bool spicy, toppings;
            public ItemJson[] items;
        }
        [Serializable] class RecipeFile { public RecipeJson[] recipes; }
#pragma warning restore 0649

        static GameDB()
        {
            Load();
        }

        static T ReadJson<T>(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null) throw new Exception($"Thiếu file dữ liệu Resources/{resourcePath}.json");
            return JsonUtility.FromJson<T>(asset.text);
        }

        static void Load()
        {
            foreach (var j in ReadJson<IngredientFile>("Data/ingredients").ingredients)
            {
                if (!Enum.TryParse(j.category, out IngCategory cat))
                {
                    Debug.LogError($"[GameDB] Nguyên liệu {j.id}: category \"{j.category}\" không hợp lệ");
                    continue;
                }
                var d = new IngredientDef
                {
                    id = j.id, name = j.name, unit = j.unit, cat = cat,
                    retailUnit = j.retailUnit, retailQty = Math.Max(1, j.retailQty), retailPrice = j.retailPrice,
                    packUnit = j.packUnit, pack = Math.Max(1, j.pack), packPrice = j.packPrice,
                    retail = (int)Math.Round((double)j.retailPrice / Math.Max(1, j.retailQty)),
                    wholesale = (int)Math.Round((double)j.packPrice / Math.Max(1, j.pack)),
                    shelfDays = Math.Max(1, j.shelfDays),
                    topping = j.topping, toppingPrice = j.toppingPrice,
                    toppingFor = string.IsNullOrEmpty(j.toppingFor) ? null
                        : j.toppingFor.Split(',').Select(x => (DishCategory)Enum.Parse(typeof(DishCategory), x.Trim())).ToArray(),
                };
                Ing[d.id] = d;
                IngList.Add(d);
            }
            if (!Ing.ContainsKey(Chili)) Debug.LogError($"[GameDB] Thiếu nguyên liệu bắt buộc \"{Chili}\" (bột ớt)");

            foreach (var j in ReadJson<RecipeFile>("Data/recipes").recipes)
            {
                if (!Enum.TryParse(j.category, out DishCategory cat))
                {
                    Debug.LogError($"[GameDB] Món {j.id}: category \"{j.category}\" không hợp lệ");
                    continue;
                }
                var missing = j.items.Where(i => !Ing.ContainsKey(i.ing)).Select(i => i.ing).ToList();
                if (missing.Count > 0)
                {
                    Debug.LogError($"[GameDB] Món {j.id} dùng nguyên liệu không tồn tại: {string.Join(", ", missing)}");
                    continue;
                }
                var r = new RecipeDef
                {
                    id = j.id, name = j.name, cat = cat, price = j.price, cookSeconds = j.cookSeconds,
                    realMinutes = j.realMinutes, spicy = j.spicy, chapter = j.chapter, note = j.note, allowToppings = j.toppings,
                    broth = string.IsNullOrEmpty(j.broth) ? null : j.broth,
                    items = j.items.Select(i => new RecipeItem { ing = i.ing, qty = Math.Max(1, i.qty) }).ToArray(),
                };
                Recipes.Add(r);
                RecipeById[r.id] = r;
            }
            Debug.Log($"[GameDB] Đã nạp {IngList.Count} nguyên liệu, {Recipes.Count} món từ JSON.");
        }

        public static IngredientDef GetIng(string id) => Ing.TryGetValue(id, out var d) ? d : null;
        public static RecipeDef GetRecipe(string id) => RecipeById.TryGetValue(id, out var r) ? r : null;

        public static IEnumerable<RecipeDef> UnlockedRecipes(int chapter) => Recipes.Where(r => r.chapter <= chapter);

        public static List<IngredientDef> UnlockedIngredients(int chapter)
        {
            var set = new HashSet<string>();
            var recipes = UnlockedRecipes(chapter).ToList();
            foreach (var r in recipes)
            {
                foreach (var it in r.items) set.Add(it.ing);
                if (r.spicy) set.Add(Chili);
                if (r.broth == "any") { set.Add(BrothKimchi); set.Add(BrothThai); }
                else if (r.HasBroth) set.Add(r.broth);
            }
            foreach (var i in IngList)
                if (recipes.Any(r => r.allowToppings && i.IsToppingFor(r.cat, chapter))) set.Add(i.id);
            return IngList.Where(i => set.Contains(i.id)).ToList();
        }

        public static List<IngredientDef> ToppingsFor(RecipeDef r, int chapter) =>
            r.allowToppings ? IngList.Where(i => i.IsToppingFor(r.cat, chapter)).ToList() : new List<IngredientDef>();

        public static int DailyToppingCount(int chapter) => Math.Min(6, 2 + chapter / 8);

        public static List<IngredientDef> DailyToppings(int day, int chapter)
        {
            var all = IngList.Where(i => i.topping > 0 && i.topping <= chapter && i.toppingFor != null).ToList();
            var picked = new List<IngredientDef>();
            foreach (var cat in new[] { DishCategory.Noodle, DishCategory.Rice, DishCategory.Drink })
            {
                var first = all.Where(i => Array.IndexOf(i.toppingFor, cat) >= 0).OrderBy(i => DayHash(day, i.id)).FirstOrDefault();
                if (first != null && !picked.Contains(first)) picked.Add(first);
            }
            int want = Math.Max(picked.Count, DailyToppingCount(chapter));
            foreach (var i in all.OrderBy(i => DayHash(day, i.id)))
            {
                if (picked.Count >= want) break;
                if (!picked.Contains(i)) picked.Add(i);
            }
            return picked;
        }

        public static List<IngredientDef> DailyToppingsFor(RecipeDef r, int day, int chapter)
        {
            var today = DailyToppings(day, chapter);
            return ToppingsFor(r, chapter).Where(today.Contains).ToList();
        }

        static uint DayHash(int day, string id)
        {
            uint h = 2166136261u;
            unchecked
            {
                foreach (char c in id) { h ^= c; h *= 16777619u; }
                h ^= (uint)(day * 2654435761u);
                h *= 16777619u;
                h ^= h >> 15;
            }
            return h;
        }

        public static long WholesaleCost(RecipeDef r, int spice = 3)
        {
            long c = 0;
            foreach (var it in r.items) c += (long)GetIng(it.ing).wholesale * it.qty;
            if (r.spicy) c += (long)GetIng(Chili).wholesale * spice;
            if (r.HasBroth) c += GetIng(r.broth == "any" ? BrothKimchi : r.broth).wholesale;
            return c;
        }

        public static string OmitName(string ing) => ing == "da" ? "đá" : ing == "duong" ? "đường" : GetIng(ing).name.ToLower();

        public static List<string> Omittable(RecipeDef r)
        {
            var list = new List<string>();
            if (r.IsDrink)
            {
                foreach (var it in r.items) if (it.ing == "da" || it.ing == "duong") list.Add(it.ing);
                return list;
            }
            bool Side(IngredientDef d) => d.cat == IngCategory.Meat || d.cat == IngCategory.Seafood || d.id == "pho_mai" || d.id == "trung";
            var sides = r.items.Skip(1).Select(it => GetIng(it.ing)).Where(Side).ToList();
            foreach (var it in r.items.Skip(1))
            {
                var d = GetIng(it.ing);
                if (d.cat == IngCategory.Vegetable || (sides.Count >= 2 && sides.Contains(d))) list.Add(d.id);
            }
            return list.Distinct().ToList();
        }

        public static string RecipeText(RecipeDef r, int spice, IList<string> extras = null, string broth = null, IList<string> omits = null)
        {
            var parts = r.items.Select(it => omits != null && omits.Contains(it.ing)
                ? $"<color=#1565C0><b>KHÔNG {GetIng(it.ing).name}</b></color>"
                : it.qty > 1 ? $"{GetIng(it.ing).name} x{it.qty}" : GetIng(it.ing).name).ToList();
            if (r.HasBroth)
                parts.Insert(Math.Min(1, parts.Count), broth != null ? $"<b>{GetIng(broth).name}</b>"
                    : r.broth == "any" ? "Nước lẩu kim chi / lẩu Thái (theo khách chọn)" : GetIng(r.broth).name);
            if (r.spicy) parts.Add(spice > 0 ? $"Bột ớt x{spice}" : "KHÔNG bột ớt (cấp 0)");
            string text = string.Join(", ", parts);
            if (extras != null && extras.Count > 0)
                text += " <color=#C2185B><b>+ THÊM: " + string.Join(", ", extras.GroupBy(x => x).Select(g =>
                    (g.Count() > 1 ? $"{GetIng(g.Key).name} x{g.Count()}" : GetIng(g.Key).name) + $" (+{Finance.Short(GetIng(g.Key).toppingPrice * g.Count())})")) +
                    " — tính thêm tiền</b></color>";
            if (omits != null && omits.Count > 0)
                text += " <color=#1565C0>(bớt nguyên liệu: giá giữ nguyên)</color>";
            return text;
        }
    }
}
