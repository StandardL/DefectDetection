namespace DefectDetection.Helpers;

public static class Commoms
{
    public static string[] labels = ["crazing", "inclusion", "pitted_surface", "rolled-in_scale", "scratches", "patches"];
    public static Dictionary<string, string> dicEng2Chi = new()
    {
        { "crazing", "裂纹" },
        { "inclusion", "夹杂物" },
        { "pitted_surface", "凹坑" },
        { "rolled-in_scale", "卷入鳞片" },
        { "scratches", "划痕" },
        { "patches", "斑点" }
    };
}
