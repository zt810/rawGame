using System;
using System.Collections.Generic;

namespace MiCay
{
    [Serializable]
    public class Batch
    {
        public string ing;
        public int qty;
        public int expiryDay;
        public long unitCost;
    }

    [Serializable]
    public class SpoilRecord
    {
        public int day;
        public string ing;
        public int qty;
        public long value;
    }

    [Serializable]
    public class PendingDelivery
    {
        public string ing;
        public int qty;
        public long unitCost;
        public bool wholesale;
        public int arriveDay;
        public float arriveTimer;
    }

    [Serializable]
    public class LedgerEntry
    {
        public int day;
        public string cat;
        public string note;
        public long amount;
    }

    [Serializable]
    public class NameCount
    {
        public string id;
        public int count;
    }

    [Serializable]
    public class Review
    {
        public int table;
        public string name;
        public int stars;
        public string text;
        public float clock;
    }

    [Serializable]
    public class DayStats
    {
        public int day;
        public int groupsServed, groupsLeft, groupsNoSeat;
        public int dishesSold, drinksSold, ruined, spiceWrong;
        public long revenue, cogs, wasteCost, spoiled, interestAccrued;
        public int ratingSum, ratingCount;
        public List<NameCount> sold = new List<NameCount>();
        public List<Review> reviews = new List<Review>();

        public void AddSold(string id, int n = 1)
        {
            foreach (var s in sold) if (s.id == id) { s.count += n; return; }
            sold.Add(new NameCount { id = id, count = n });
        }
    }

    [Serializable]
    public class TaxBill
    {
        public int id;
        public int fromDay, toDay, dueDay;
        public long revenue, expenses, annualized;
        public long vat, pit;
        public TaxMethod method;
        public bool splitChapters;
        public bool aboveThreshold;
        public bool paid, enforced;
        public int paidDay;
        public long interestPaid, finePaid;
        public long Total => vat + pit;
    }

    [Serializable]
    public class Debt
    {
        public const string Family = "family", Collector = "collector", Bank = "bank", Shark = "shark";

        public string id, name, creditor;
        public string kind = Collector;
        public long remaining;
        public float interestPerPeriod;
        public int periodDays;
        public long installment;
        public int nextDueDay;
        public long paidThisPeriod;
        public int missed;
        public bool mandatory;
        public int dueDay;
    }

    [Serializable]
    public class Mastery
    {
        public string recipe;
        public int count;
    }

    [Serializable]
    public class WageEntry
    {
        public string who;
        public int day;
        public long amount;
    }

    [Serializable]
    public class DepartedDebtor
    {
        public string who;
        public int dueDay;
    }

    [Serializable]
    public class DayPnL
    {
        public int day;
        public long net;
    }

    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 3;
        public int version = CurrentVersion;
        public long cash;
        public int day = 1;
        public int chapter = 1;
        public int chapterStartDay = 1;
        public int maxDayReached = 1;
        public int saveSeq;
        public int stage;
        public int endOutcome;
        public string endReason = "";
        public float reputation = 3f;

        public const int StartTables = 8;
        public int stoves = 3, drinkBars = 1, tables = StartTables;
        public int drinkMachines;
        public List<int> stoveTiers = new List<int> { 0, 0, 0 };
        public List<int> stoveUpgraded = new List<int> { 0, 0, 0 };
        public List<int> stoveDays = new List<int> { 1, 1, 1 };
        public List<int> barTiers = new List<int> { 0 };
        public List<int> barUpgraded = new List<int> { 0 };
        public List<int> barDays = new List<int> { 1 };
        public List<int> tableSlots = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        public List<string> helpers = new List<string>();
        public List<int> helperAreas = new List<int>();
        public List<int> helperDaysWorked = new List<int>();
        public List<WageEntry> wages = new List<WageEntry>();
        public List<DepartedDebtor> departed = new List<DepartedDebtor>();
        public int staff;
        public long staffWage;
        public long rentPerDay, utilityPerDay;

        public int priceAdjust;
        public bool showRecipeHint = true;
        public int closedDay = -1;
        public int evasionCount;
        public int negativeCashDays;
        public int nextBillId = 1;
        public bool finished;

        public int tutorialStep = -1;
        public string tutLesson = "";
        public string tutIng = "";
        public string tutResume = "";
        public int tutResumeStep;
        public int tutStep;
        public int tutLastDay;
        public List<string> tutDone = new List<string>();

        public int violationPoints;
        public int lastViolationDay;
        public int prisonCount;
        public int shadyUntil;
        public bool fireSafetyOk;
        public bool eInvoiceOk;
        public bool sidewalkPermit;

        public float tempCustomerMul = 1f; public int tempCustomerUntil;
        public float tempPriceMul = 1f;    public int tempPriceUntil;
        public float tempDelayMul = 1f;    public int tempDelayUntil;
        public int brokenStoveUntil;
        public int staffAwayUntil;
        public int nextBankId = 1;

        public List<Batch> inventory = new List<Batch>();
        public List<SpoilRecord> spoilLog = new List<SpoilRecord>();
        public List<PendingDelivery> deliveries = new List<PendingDelivery>();
        public List<LedgerEntry> ledger = new List<LedgerEntry>();
        public List<DayStats> stats = new List<DayStats>();
        public List<TaxBill> taxBills = new List<TaxBill>();
        public List<Debt> debts = new List<Debt>();
        public List<Mastery> mastery = new List<Mastery>();
        public List<string> disabledRecipes = new List<string>();
        public List<string> firedEvents = new List<string>();
        public List<DayPnL> dailyPnl = new List<DayPnL>();

        public int ChapterDay => day - chapterStartDay + 1;

        public DayStats Today
        {
            get
            {
                for (int i = stats.Count - 1; i >= 0; i--) if (stats[i].day == day) return stats[i];
                var s = new DayStats { day = day };
                stats.Add(s);
                return s;
            }
        }

        public int GetMastery(string recipe)
        {
            foreach (var m in mastery) if (m.recipe == recipe) return m.count;
            return 0;
        }

        public void AddMastery(string recipe)
        {
            foreach (var m in mastery) if (m.recipe == recipe) { m.count++; return; }
            mastery.Add(new Mastery { recipe = recipe, count = 1 });
        }

        public Debt GetDebt(string id)
        {
            foreach (var d in debts) if (d.id == id) return d;
            return null;
        }

        public float PriceFactor => priceAdjust < 0 ? 0.9f : priceAdjust > 0 ? 1.1f : 1f;

        public float CustomerMul => day <= tempCustomerUntil ? tempCustomerMul : 1f;
        public float TempPriceMul => day <= tempPriceUntil ? tempPriceMul : 1f;
        public float TempDelayMul => day <= tempDelayUntil ? tempDelayMul : 1f;
        public bool StoveBroken => day <= brokenStoveUntil;
        public bool ShadyGoods => day <= shadyUntil;
        public int ActiveStaff => day <= staffAwayUntil ? 0 : staff;
        public int DrinkSlots
        {
            get
            {
                int n = 0;
                foreach (var t in barTiers) n += t == 0 ? 1 : 2;
                return n;
            }
        }

        public void SyncCounts()
        {
            drinkBars = 0;
            foreach (var t in barTiers) if (t == 0) drinkBars++;
            drinkMachines = barTiers.Count - drinkBars;
            tables = tableSlots.Count;
        }

        public bool HasTable(int t) => tableSlots.Contains(t);

        public bool AreaHasTables(int area)
        {
            foreach (var t in tableSlots) if (t / CustomerSystem.TablesPerArea == area) return true;
            return false;
        }
    }
}
