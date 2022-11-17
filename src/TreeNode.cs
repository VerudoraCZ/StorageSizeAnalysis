using System.Collections;
using System.Drawing;
using System.Text;
using Pastel;
using static System.Drawing.Color;

namespace StorageSizeAnalysis;

public class TreeNode : IEnumerable<TreeNode>
{
    public readonly string Id;
    private Dictionary<string, TreeNode> _children = new();

    public TreeNode(string id, long isolatedSize = 0, long totalSize = 0)
    {
        Id = id;
        IsolatedSize = isolatedSize;
        TotalSize = totalSize;
    }

    public long IsolatedSize { get; set; }
    public long TotalSize { get; set; }
    public TreeNode? Parent { get; private set; }

    public bool HasChildren => _children.Count > 0;

    public int Count => _children.Count;

    public IEnumerator<TreeNode> GetEnumerator()
    {
        return _children.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void SetParent(TreeNode? parent)
    {
        Parent = parent;
    }

    public bool HasChild(string id)
    {
        return _children.ContainsKey(id);
    }

    public TreeNode GetChild(string id)
    {
        return _children[id];
    }

    public List<TreeNode> GetChildren()
    {
        return _children.Values.ToList();
    }

    public Dictionary<string, TreeNode> GetChildrenDict()
    {
        return _children;
    }

    public void SetChildrenDict(Dictionary<string, TreeNode> children)
    {
        _children.Clear();

        foreach (var child in children)
        {
            _children.Add(child.Key, child.Value);
            child.Value.SetParent(this);
        }
    }

    //sort children by total size
    private void SortChildren()
    {
        _children = _children.OrderByDescending(x => x.Value.TotalSize).ToDictionary(x => x.Key, x => x.Value);
    }

    //sort children and their children by total size in specified depth
    public void SortChildren(int depth)
    {
        if (depth == 0)
        {
            SortChildren();
            return;
        }

        foreach (var child in _children) child.Value.SortChildren(depth - 1);

        SortChildren();
    }

    public void Add(TreeNode item)
    {
        item.Parent?._children.Remove(item.Id);

        item.Parent = this;
        _children.Add(item.Id, item);
    }

    public void Clear()
    {
        _children.Clear();
    }
}

public static class TreeNodeExtensions
{
    public static void ExportToJson(this TreeNode node, string path, int depth)
    {
        var json = node.ToJson(depth);
        File.WriteAllText(path, json);
    }

    private static string ToJson(this TreeNode node, int depth)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"id\": \"{node.Id}\",");
        sb.Append($"\"isolatedSize\": {node.IsolatedSize},");
        sb.Append($"\"totalSize\": {node.TotalSize},");
        sb.Append($"\"children\": [");

        var children = node.GetChildren();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            sb.Append(child.ToJson(depth - 1));
            if (i < children.Count - 1) sb.Append(",");
        }

        sb.Append("]}");
        return sb.ToString();
    }


    private static List<List<TreeNode>> SplitByGeneration(this TreeNode root)
    {
        var result = new List<List<TreeNode>>();

        var currentGeneration = new List<TreeNode> {root};
        while (currentGeneration.Count > 0)
        {
            result.Add(currentGeneration);
            currentGeneration = currentGeneration.SelectMany(x => x).ToList();
        }

        return result;
    }

    private static string BytesToString(this long byteCount)
    {
        string[] suf = {"B", "KB", "MB", "GB", "TB", "PB", "EB"}; //Longs run out around EB
        if (byteCount == 0) return $"0{suf[0]}";

        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return $"{Math.Sign(byteCount) * num}{suf[place]}";
    }

    private static string ColorizeSize(this TreeNode child)
    {
        Color color;
        if (child.Parent!.TotalSize == 0)
        {
            color = White;
        }
        else
        {
            var percentage = child.TotalSize / (double) child.Parent!.TotalSize * 100;
            //colorize the percentage
            color = percentage switch
            {
                <= 20 => Green,
                <= 40 => YellowGreen,
                <= 60 => Yellow,
                <= 80 => Orange,
                <= 100 => Red,
                _ => Gray
            };
        }

        return child.TotalSize.BytesToString().Pastel(color);
    }

    private static TreeNode GetRoot(this TreeNode node)
    {
        while (node.Parent != null) node = node.Parent;

        return node;
    }

    public static void Print(this TreeNode node, int depth = -1, string indent = "", bool last = true,
        bool first = true)
    {
        //get current line length
        var nId = node.Id.Pastel(Yellow);
        if (node.GetRoot().GetEnds().Contains(node)) nId = node.Id.Pastel(Orange);

        var nSize = node.Parent is null ? node.TotalSize.BytesToString().Pastel(Blue) : node.ColorizeSize();

        if (last && !first)
        {
            Console.WriteLine($"{indent}└───[ {nId} ]---[ {nSize} ]");
            indent += "    ";
        }
        else if (first)
        {
            Console.WriteLine($"{indent} [ {nId} ]---[ {nSize} ]");
            indent += " ";
        }
        else
        {
            Console.WriteLine($"{indent}├───[ {nId} ]---[ {nSize} ]");
            indent += "│   ";
        }

        if (depth == -1)
        {
            for (var i = 0; i < node.Count; i++)
                node.GetChildren()[i].Print(indent: indent, last: i == node.Count - 1, first: false);
        }
        else
        {
            if (depth <= 0) return;
            for (var i = 0; i < node.Count; i++)
                node.GetChildren()[i].Print(depth - 1, indent, i == node.Count - 1, false);
        }
    }

    //Get child with most sub children recursively
    public static TreeNode GetChildWithMostSubChildren(this TreeNode node)
    {
        if (!node.HasChildren)
            return node;

        var max = node.GetChild(node.GetChildren()[0].Id);
        foreach (var child in node.GetChildren())
        {
            var childWithMostSubChildren = child.GetChildWithMostSubChildren();
            if (childWithMostSubChildren.Count > max.Count)
                max = childWithMostSubChildren;
        }

        return max;
    }

    //Get Sum of all children Sizes recursively
    public static long GetSumOfAllChildrenSizes(this TreeNode node)
    {
        if (!node.HasChildren) return node.IsolatedSize;

        var sum = node.IsolatedSize;
        foreach (var child in node.GetChildren()) sum += child.GetSumOfAllChildrenSizes();

        return sum;
    }

    private static long GetSumOfChildrenTotalSizes(this TreeNode node)
    {
        return !node.HasChildren ? 0 : node.GetChildren().Sum(child => child.TotalSize);
    }

    //Get TreeNode from Path
    public static TreeNode GetNodeFromPath(this TreeNode node, string path)
    {
        var pathParts = path.Split('\\');
        foreach (var pathPart in pathParts)
            if (node.HasChildren)
            {
                node = node.GetChild(pathPart);
            }
            else
            {
                Console.WriteLine(nameof(GetNodeFromPath));
                return null!;
            }

        return node;
    }

    //Get Path from TreeNode without root
    public static string GetPathFromNode(this TreeNode node)
    {
        var path = node.Id;
        while (node.Parent is {Parent: { }})
        {
            node = node.Parent;
            path = node.Id + "\\" + path;
        }

        return path;
    }

    //    AI Generated comment:
    //
    //    1.  First we are going to get a list of all the ending nodes.  In this case, an ending node is one with no children.
    //    2.  Next we are going to split up all the nodes by generation.  This allows us to iterate a certain way later on.
    //    3.  Finally, we are going to iterate through all the generations, but in reverse order.  Why?  Because we need to iterate
    //    through the generations in order of distance from the root.  We are then going to iterate through all the nodes in that
    //    generation and either assign the node's total size to the node's isolated size (because the node is an ending node), or
    //    we are going to add the sum of all children's total sizes to the node's isolated size.
    public static void CalculateTotalSize(this TreeNode node)
    {
        var endingNodes = node.GetEnds();
        var generations = node.SplitByGeneration();
        for (var i = generations.Count - 1; i >= 1; i--)
        for (var j = 0; j < generations[i].Count; j++)
            if (endingNodes.Contains(generations[i][j]))
                generations[i][j].TotalSize = generations[i][j].IsolatedSize;
            else
                generations[i][j].TotalSize =
                    generations[i][j].GetSumOfChildrenTotalSizes() + generations[i][j].IsolatedSize;
    }

    // ReSharper disable once UnusedMember.Local
    private static void EliminateSiblings(this IList<TreeNode> ends)
    {
        for (var i = 0; i < ends.Count; i++)
        for (var j = i + 1; j < ends.Count; j++)
        {
            if (ends[i].Parent is null) continue;
            if (ends[i].Parent != ends[j].Parent) continue;
            ends.RemoveAt(j);
            j--;
        }
    }


    //    1.  It's creating an extension method to the TreeNode class called AddSafe.
    //    2.  It accepts a TreeNode parameter called child.
    //    3.  It checks to see if the child.Id exists in the parent's GetChildrenDictionary property.
    //    4.  If it does exist, it gets the existing child from the GetChildrenDictionary property.
    //    5.  It then uses foreach to iterate through each grandchild of the child parameter.
    //    6.  For each grandchild, it calls the AddSafe extension method on the existing child.  This recursively checks each grandchild's Id until it reaches the end of the chain.
    //    7.  If the child.Id doesn't exist, it simply adds the child to the parent.
    //
    //    It's that easy!  With this extension method, you can add children to TreeNodes without having to worry about duplicates.  It's up to you to decide how you want to handle duplicates.  In my case, I want to keep the first duplicate and simply add any grandchild nodes to the existing child.  You could easily modify the extension method to handle duplicates differently.  For example, you might want to prompt the user before adding duplicates or combine the child nodes with the existing child node.
    //    I hope you find this useful.  If you have any questions or comments, please let me know.  Thanks!
    public static void AddSafe(this TreeNode parent, TreeNode child)
    {
        if (parent.HasChild(child.Id))
        {
            var existingChild = parent.GetChild(child.Id);
            foreach (var grandchild in child.GetChildren()) existingChild.AddSafe(grandchild);
        }
        else
        {
            parent.Add(child);
        }
    }

    #region Node Population

    //Populate TreeNode from from Path
    public static void PopulateNodeFromPath(this TreeNode node, string path, long size)
    {
        var pathParts = path.Split('\\');
        foreach (var pathPart in pathParts)
            if (node.HasChild(pathPart))
            {
                node = node.GetChild(pathPart);
            }
            else
            {
                var newNode = new TreeNode(pathPart);
                node.Add(newNode);
                node = newNode;
            }

        node.IsolatedSize = size;
    }

    //Populate TreeNode from Path Array
    public static TreeNode PopulateNodeFromPath(this TreeNode node, string[] paths, long[] sizes)
    {
        for (var i = 0; i < paths.Length; i++) node.PopulateNodeFromPath(paths[i], sizes[i]);

        return node;
    }

    //Populate TreeNode from Path IEnumerable
    public static TreeNode PopulateNodeFromPath(this TreeNode node, IEnumerable<string> paths, IEnumerable<long> sizes)
    {
        var pathsList = paths.ToList();
        var sizesList = sizes.ToList();
        if (pathsList.Count != sizesList.Count)
            throw new ArgumentException($"{nameof(PopulateNodeFromPath)} - Paths and Sizes must have the same length");


        using var sLEnmr = sizesList.GetEnumerator();
        using var pLEnmr = pathsList.GetEnumerator();
        while (sLEnmr.MoveNext() && pLEnmr.MoveNext()) node.PopulateNodeFromPath(pLEnmr.Current, sLEnmr.Current);

        return node;
    }

    //Create TreeNode from Path
    public static TreeNode CreateNodeFromPath(string path, long size)
    {
        var node = new TreeNode("ROOT");
        node.PopulateNodeFromPath(path, size);
        if (node.HasChildren) node = node.GetChildren()[0];

        return node;
    }

    #endregion

    #region Node Sorting

    public static TreeNode SortBySize(this TreeNode node, bool descending = false)
    {
        var sortedChildren = node.GetChildren().OrderByDescending(x => x.IsolatedSize).ToList();
        if (descending) sortedChildren.Reverse();

        node.Clear();
        foreach (var child in sortedChildren) node.Add(child);

        return node;
    }

    //Get all ends of the tree
    private static List<TreeNode> GetEnds(this TreeNode node)
    {
        var ends = new List<TreeNode>();
        if (!node.HasChildren)
        {
            ends.Add(node);
            return ends;
        }

        foreach (var child in node.GetChildren()) ends.AddRange(child.GetEnds());

        return ends;
    }

    #endregion
}