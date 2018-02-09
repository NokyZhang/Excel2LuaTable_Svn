using System.Collections.Generic;

public class RefactoryTable
{
    public struct RefactoryTableSetting
    {
        public string tableName;
        public Dictionary<string, string> RefactoryKV;

        /// <summary>
        /// kvs.Length must be a multiple of 2
        /// </summary>
        /// <param name="tname"></param>
        /// <param name="kvs"></param>
        public RefactoryTableSetting(string tname, params string[] kvs)
        {
            tableName = tname;
            RefactoryKV = new Dictionary<string, string>();
            if (kvs.Length % 2 == 0)
            {
                for (int i = 0; i < kvs.Length; i += 2)
                    RefactoryKV.Add(kvs[i], kvs[i + 1]);
            }
        }
    }

    public static Dictionary<string, List<RefactoryTableSetting>> data = new Dictionary<string, List<RefactoryTableSetting>>()
    {
        { "Rune",
            new List<RefactoryTableSetting>()
            {
                new RefactoryTableSetting("Table_Rune_11", "Swordman_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_12", "Crusader_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_21", "Magician_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_31", "Thief_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_32", "Rogue_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_41", "Archer_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_51", "Acolyte_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_52", "Monk_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_61", "Merchant_Attr", "Attr"),
                new RefactoryTableSetting("Table_Rune_62", "Alchemist_Attr", "Attr"),
            }
        }
    };
}