using Newtonsoft.Json;

namespace StorageSizeAnalysis;

public static class Json
{
    //Serialize TreeNode and it's properties to JSON
    public static string Serialize(TreeNode node)
    {
        return JsonConvert.SerializeObject(node, Formatting.Indented);
    }
}