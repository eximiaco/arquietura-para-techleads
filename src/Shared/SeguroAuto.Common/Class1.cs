namespace SeguroAuto.Common;

public static class EnvironmentHelper
{
    public static string GetDbPath(string defaultValue = "./data/legacy.db")
    {
        return Environment.GetEnvironmentVariable("DB_PATH") ?? defaultValue;
    }

    public static int GetDatasetSeed(int defaultValue = 1001)
    {
        var seedStr = Environment.GetEnvironmentVariable("DATASET_SEED");
        return int.TryParse(seedStr, out var seed) ? seed : defaultValue;
    }

    public static string GetDatasetProfile(string defaultValue = "legacy")
    {
        return Environment.GetEnvironmentVariable("DATASET_PROFILE") ?? defaultValue;
    }
}
