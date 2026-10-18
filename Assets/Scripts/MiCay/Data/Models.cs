using System.Collections.Generic;
using UnityEngine;

namespace MiCay
{
    public enum DishCategory { Noodle, Rice, Drink }
    public enum IngCategory { Base, Meat, Seafood, Vegetable, Sauce, RiceSide, Drink }
    public enum StationType { Stove, DrinkBar }
    public enum TaxMethod { RevenuePercent, ProfitPercent }

    public class IngredientDef
    {
        public string id, name, unit;
        public IngCategory cat;
        public int retail;
        public int wholesale;
        public int pack;
        public string retailUnit, packUnit;
        public int retailQty, retailPrice, packPrice;
        public int shelfDays;
        public int topping;
        public DishCategory[] toppingFor;
        public int toppingPrice;

        public bool IsDrink => cat == IngCategory.Drink;
        public bool IsToppingFor(DishCategory c, int chapter) =>
            topping > 0 && topping <= chapter && toppingFor != null && System.Array.IndexOf(toppingFor, c) >= 0;

        public float OrderSeconds => 60f;
    }

    public struct RecipeItem
    {
        public string ing;
        public int qty;
    }

    public class RecipeDef
    {
        public string id, name, note;
        public DishCategory cat;
        public int price;
        public float cookSeconds;
        public int realMinutes;
        public bool spicy;
        public int chapter;
        public bool allowToppings;
        public string broth;
        public bool HasBroth => !string.IsNullOrEmpty(broth);
        public RecipeItem[] items;

        public bool IsDrink => cat == DishCategory.Drink;
        public StationType Station => IsDrink ? StationType.DrinkBar : StationType.Stove;
    }

    public class CharacterDef
    {
        public string id, name, role;
        public Color color;
        public string sprite;
        public CharacterDef(string id, string name, string role, Color color, string sprite = null)
        {
            this.id = id; this.name = name; this.role = role; this.color = color; this.sprite = sprite ?? id;
        }
    }

    public struct DialogueLine
    {
        public string who;
        public string text;
        public DialogueLine(string who, string text) { this.who = who; this.text = text; }
    }

    public enum EventAction
    {
        None,
        AddDebt,
        GiveCash,
        RentChange,
        UtilityChange,
        HireStaff,
        TaxAudit,
        ReputationChange,
        Theft,
        Loss,
        PersonalExpense,
        Expense,
        Invest,
        StoveBroken,
        CustomerMod,
        PriceSpike,
        SupplierDelay,
        StaffAway,
        StaffRaise,
        ClosedDay,
        ShadySupplier,
        HealthInspection,
        FireInspection,
        EInvoice,
        Sidewalk,
        Scam,
        LoanShark,
        SpillBroth,
    }

    public class DayEvent
    {
        public int day;
        public bool atEnd;
        public DialogueLine[] lines;
        public EventAction action;
        public long amount;
        public int days;
        public string param;
    }

    public class ChapterDef
    {
        public int number;
        public string title, subtitle, goalText;
        public int days;
        public bool hard;

        public long? goalRevenue;
        public long? goalNetProfit;
        public float? goalReputation;
        public string goalDebtCleared;
        public bool goalNoOverdueTax;
        public long? goalCash;
        public int? goalServed;

        public float customerRate = 1f;
        public float patienceMul = 1f;
        public float ingredientPriceMul = 1f;
        public float orderDelayMul = 1f;
        public float shortageChance = 0f;
        public long rentPerDay = 200_000;
        public long utilityPerDay = 150_000;

        public long taxThresholdYear = 1_000_000_000;
        public bool forceAboveThreshold;
        public TaxMethod taxMethod = TaxMethod.RevenuePercent;
        public float vatRate = 0.03f;
        public float pitRate = 0.015f;

        public DialogueLine[] intro, outro, fail;
        public List<DayEvent> events = new List<DayEvent>();
    }
}
