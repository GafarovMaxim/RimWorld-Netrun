using RimWorld;
using Verse;

namespace Netrun;

/// <summary>Vanilla pawn records Netrun keeps (the Records tab); achievements and the dive traits read them too.</summary>
[DefOf]
public static class NetrunRecords
{
    static NetrunRecords() => DefOfHelper.EnsureInitializedInCtor(typeof(NetrunRecords));

    public static RecordDef NR_Record_Dives;
    public static RecordDef NR_Record_CleanExits;
    public static RecordDef NR_Record_BlackICEPassed;
    public static RecordDef NR_Record_CorpCores;
    public static RecordDef NR_Record_NetComas;
    public static RecordDef NR_Record_AIsAwakened;

    public static void Add(Pawn pawn, RecordDef record)
    {
        if (pawn?.records != null && record != null)
            pawn.records.Increment(record);
    }
}
