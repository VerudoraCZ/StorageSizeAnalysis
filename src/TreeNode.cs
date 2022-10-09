using System.Collections;
using System.Diagnostics;
using Pastel;
using static System.Drawing.Color;

namespace StorageSizeAnalysis;

public class TreeNode : IEnumerable<TreeNode>
{
    private Dictionary<string, TreeNode> _children = new();

    public readonly string Id;
    public long IsolatedSize { get; set; }
    public long TotalSize { get; set; }
    public TreeNode? Parent { get; private set; }

    public TreeNode(string id, long isolatedSize = 0, long totalSize = 0)
    {
        Id = id;
        IsolatedSize = isolatedSize;
        TotalSize = totalSize;
    }

    public void SetParent(TreeNode? parent)
    {
        Parent = parent;
    }
    
    public bool HasChildren => _children.Count > 0;
    
    public bool HasChild(string id) => _children.ContainsKey(id);
    
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
    public void SortChildren()
    {
        _children = _children.OrderByDescending(x => x.Value.TotalSize).ToDictionary(x => x.Key, x => x.Value);
    }

    public void Add(TreeNode item)
    {
        if (item.Parent != null)
        {
            item.Parent._children.Remove(item.Id);
        }

        item.Parent = this;
        _children.Add(item.Id, item);
    }

    public IEnumerator<TreeNode> GetEnumerator()
    {
        return _children.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => _children.Count;
    
    public void Clear()
    {
        _children.Clear();
    }
}







public static class TreeNodeExtensions
{
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
        string[] suf = {" B", " KB", " MB", " GB", " TB", " PB", " EB"}; //Longs run out around EB
        if (byteCount == 0) return "0" + suf[0];
        
        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return Math.Sign(byteCount) * num + suf[place];
    }

    private static TreeNode GetRoot(this TreeNode node)
    {
        while (node.Parent != null)
        {
            node = node.Parent;
        }

        return node;
    }
    
    public static void Print(this TreeNode node, int depth = -1, string indent = "", bool last = true, bool first = true)
    {
        //get current line length
        var nId = node.Id.Pastel(OrangeRed);
        if (node.GetRoot().GetEnds().Contains(node))
        {
            nId = node.Id.Pastel(Orange);
        }
        //var nSize = node.GetSumOfAllChildrenSizes().BytesToString().Pastel(Color.DarkOrange);
        var nSize = node.TotalSize.BytesToString().Pastel(Blue);
        
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
        
        if (depth == -1) {
            for (var i = 0; i < node.Count; i++)
                node.GetChildren()[i].Print(indent: indent, last: i == node.Count - 1, first: false);
        }
        else
        {
            if (depth <= 0) return;
            for (var i = 0; i < node.Count; i++)
                node.GetChildren()[i].Print(depth: depth - 1, indent: indent, last: i == node.Count - 1, first: false);
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
        foreach (var child in node.GetChildren())
        {
            sum += child.GetSumOfAllChildrenSizes();
        }

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
        {
            if (node.HasChildren)
            {
                node = node.GetChild(pathPart);
            }
            else
            {
                Console.WriteLine(nameof(GetNodeFromPath));
                return null!;
            }
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
    
    #region Node Population

        //Populate TreeNode from from Path
        public static void PopulateNodeFromPath(this TreeNode node, string path, long size)
        {
            var pathParts = path.Split('\\');
            foreach (var pathPart in pathParts)
            {
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
            }

            node.IsolatedSize = size;
        }

        //Populate TreeNode from Path Array
        public static TreeNode PopulateNodeFromPath(this TreeNode node, string[] paths, long[] sizes)
        {
            for (var i = 0; i < paths.Length; i++)
            {
                node.PopulateNodeFromPath(paths[i], sizes[i]);
            }

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
            while (sLEnmr.MoveNext() && pLEnmr.MoveNext())
            {
                node.PopulateNodeFromPath(pLEnmr.Current, sLEnmr.Current);
            }

            return node;
        }
        
        //Create TreeNode from Path
        public static TreeNode CreateNodeFromPath(string path, long size)
        {
            var node = new TreeNode("ROOT");
            node.PopulateNodeFromPath(path, size);
            if (node.HasChildren)
            {
                node = node.GetChildren()[0];
            }
            return node;
        }

    #endregion

    #region Node Sorting

        public static TreeNode SortBySize(this TreeNode node, bool descending = false)
        {
            var sortedChildren = node.GetChildren().OrderByDescending(x => x.IsolatedSize).ToList();
            if (descending) sortedChildren.Reverse();

            node.Clear();
            foreach (var child in sortedChildren)
            {
                node.Add(child);
            }

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

            foreach (var child in node.GetChildren())
            {
                ends.AddRange(child.GetEnds());
            }

            return ends;
        }

    #endregion

    

    public static void CalculateTotalSize(this TreeNode node)
    {
        var endingNodes = node.GetEnds();
        var generations = node.SplitByGeneration();
        for (var i = generations.Count - 1; i >= 1; i--)
        {
            for (var j = 0; j < generations[i].Count; j++)
            {
                if (endingNodes.Contains(generations[i][j]))
                {
                    generations[i][j].TotalSize = generations[i][j].IsolatedSize;
                }
                else
                {
                    generations[i][j].TotalSize = generations[i][j].GetSumOfChildrenTotalSizes() + generations[i][j].IsolatedSize;
                }
            }
        }
    }
    
    /*private static void EliminateSiblings(this List<TreeNode> ends)
    {
        for (var i = 0; i < ends.Count; i++)
        {
            for (var j = i + 1; j < ends.Count; j++)
            {
                if (ends[i].Parent == null) continue;
                if (ends[i].Parent == ends[j].Parent)
                {
                    ends.RemoveAt(j);
                    j--;
                }
            }
        }
    }*/


    public static void AddSafe(this TreeNode parent, TreeNode child)
    {
        // recursively check for child.Id in parent.GetChildrenDictionary keys
        if (parent.HasChild(child.Id))
        {
            // if child.Id is found, get the child from the dictionary
            var existingChild = parent.GetChild(child.Id);
            // add the child's children to the existing child
            foreach (var grandchild in child.GetChildren())
            {
                existingChild.AddSafe(grandchild);
            }
        }
        else
        {
            // if child.Id is not found, add the child to the parent
            parent.Add(child);
        }
    }
}
