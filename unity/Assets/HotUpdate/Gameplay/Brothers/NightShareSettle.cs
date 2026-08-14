namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 三人夜间出逃结算预埋（不做联机）。
    /// Share = 平分；StealFlee = 独吞后跑路（本地模拟用）。
    /// </summary>
    public enum NightSettleChoice
    {
        Share,
        StealFlee
    }

    public static class NightShareSettle
    {
        public const string RuleVersion = "v1-preburied";

        public static string Describe(NightSettleChoice choice)
        {
            return choice == NightSettleChoice.Share
                ? "三人分赃：人情/培养点均分，声望小幅+"
                : "偷跑独吞：本人培养点×2，人情-，下次招集质量下降";
        }

        public static void ApplyLocalSim(NightSettleChoice choice, MetaProgress meta, int potBase, int favorBase)
        {
            if (meta == null) return;
            if (choice == NightSettleChoice.Share)
            {
                meta.AddTrain(potBase);
                meta.AddFavor(favorBase);
                meta.AddRenown(1);
            }
            else
            {
                meta.AddTrain(potBase * 2);
                meta.AddFavorRaw(-1);
            }
        }
    }
}
