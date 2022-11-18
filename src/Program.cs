using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Pastel;
using static System.Drawing.Color;

namespace StorageSizeAnalysis;

[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
internal static class Program
{
    private static string[] _excluded = null!;

    public static void Main(string[] args)
    {
        CheckArguments(args);
        LoadExcludedDirList();

        Console.CancelKeyPress += ConsoleCancelKeyPressEvent;

        var tree = new TreeNode("ROOT");
        if (args[0].EndsWith('\\')) args[0] = args[0].TrimEnd('\\');

        var dirs = CombineAllDirectoriesToList(args);
        CombineDirectoriesAndSizes(dirs, tree);

        var stp = new Stopwatch();

        #region tree.CalculateTotalSize();

        stp.Start();
        tree.CalculateTotalSize();
        stp.Stop();
        Console.WriteLine(
            $"Calculated total size for each directory. ({Math.Round(stp.ElapsedMilliseconds / 1000.0D, 2)}s)"
                .Pastel(LimeGreen));
        stp.Reset();

        #endregion

        var argRoot = tree;
        if (tree.HasChildren)
        {
            argRoot = tree.GetNodeFromPath(args[0]);
            argRoot.SetParent(null);
        }

        #region argRoot.SortChildren();

        stp.Start();
        argRoot.SortChildren(int.TryParse(args[2], out var sortType) ? sortType : 0);
        stp.Stop();
        Console.WriteLine($"Sorted tree structure by total size. ({Math.Round(stp.ElapsedMilliseconds / 1000.0D, 2)}s)"
            .Pastel(LimeGreen));
        stp.Reset();

        #endregion

        #region PrintDirectoryTree(args, argRoot);

        stp.Start();
        PrintDirectoryTree(args, argRoot);
        stp.Stop();
        Console.WriteLine(Environment.NewLine +
                          $"Printed tree structure to std.output. ({Math.Round(stp.ElapsedMilliseconds / 1000.0D, 2)}s)"
                              .Pastel(LimeGreen));

        #endregion

        PrintEndingMessage(dirs, tree);

        tree.ExportToJson("jsonExport.json", 1000000);

        SaveExcludedDirList();
    }


    //check if directory is symlink
    private static bool IsSymbolicLink(string dir)
    {
        var dirInfo = new DirectoryInfo(dir);
        return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    //check if directory is hardlink
    private static bool IsHardLink(string dir)
    {
        var dirInfo = new DirectoryInfo(dir);
        return dirInfo.Attributes.HasFlag(FileAttributes.Offline);
    }

    //check if directory is system
    private static bool IsSystem(string dir)
    {
        var dirInfo = new DirectoryInfo(dir);
        return dirInfo.Attributes.HasFlag(FileAttributes.System);
    }

    private static void ConsoleCancelKeyPressEvent(object? sender, ConsoleCancelEventArgs e)
    {
        SaveExcludedDirList();
        Console.WriteLine("Exiting...");
        Console.WriteLine("");
        Environment.Exit(0);
    }

    private static void PrintEndingMessage(ICollection dirs, TreeNode tree)
    {
        Console.WriteLine();
        Console.WriteLine($"Total directories scanned: {dirs.Count.ToString().Pastel(LimeGreen)}");
        Console.WriteLine($"Total Size: {tree.GetSumOfAllChildrenSizes().BytesToString().Pastel(LimeGreen)}");
    }

    private static List<string> CombineAllDirectoriesToList(IReadOnlyList<string> args)
    {
        var stp = new Stopwatch();
        try
        {
            stp.Start();

            var dirs = new List<string>();
            var dirTasks = GetDirectories(args[0], int.Parse(args[1]) - 1);
            Task.WaitAll(dirTasks.ToArray<Task>());
            foreach (var dirTask in dirTasks) dirs.AddRange(dirTask.Result);

            return dirs;
        }
        finally
        {
            stp.Stop();
            Console.WriteLine(
                $"All directories fetched. ({Math.Round(stp.ElapsedMilliseconds / 1000.0D, 2)}s)".Pastel(LimeGreen));
        }
    }

    private static void CombineDirectoriesAndSizes(List<string> dirs, TreeNode tree)
    {
        var stp = new Stopwatch();
        try
        {
            stp.Start();

            var tasks = CompileDirsAndSizes(dirs.ToArray());
            Task.WaitAll(tasks.ToArray<Task>());
            foreach (var treeNode in tasks.SelectMany(task => task.Result)) tree.AddSafe(treeNode);
        }
        finally
        {
            stp.Stop();
            Console.WriteLine(
                $"Directory data combined. ({Math.Round(stp.ElapsedMilliseconds / 1000.0D, 2)}s)".Pastel(LimeGreen));
        }
    }

    private static void PrintDirectoryTree(IReadOnlyList<string> args, TreeNode argRoot)
    {
        var depth = int.Parse(args[2]);
        if (depth == 0)
            argRoot.Print();
        else
            argRoot.Print(depth);
    }

    private static void CheckArguments(IList<string> args)
    {
        if (args.Count < 3) PrintHelp("Not enough arguments.");
        if (!args[0].EndsWith('\\')) args[0] += '\\';
        if (!Directory.Exists(args[0])) PrintHelp("Invalid directory path.");
        if (!int.TryParse(args[1], out var depth)) PrintHelp("Invalid depth.");
        if (!int.TryParse(args[2], out var printDepth)) PrintHelp("Invalid print depth.");
        if (depth < 1) PrintHelp("Depth must be greater than 0.");
        if (printDepth < 0) PrintHelp("Print depth must be greater than or equal to 0.");
    }

    private static void SaveExcludedDirList()
    {
        try
        {
            File.WriteAllLines("excluded.txt", _excluded);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not save excluded directory list. ({e})".Pastel(Red));
        }
    }

    private static void LoadExcludedDirList()
    {
        var stp = new Stopwatch();
        try
        {
            stp.Start();
            if (File.Exists("excluded.txt"))
            {
                _excluded = File.ReadAllLines("excluded.txt");
            }
            else
            {
                File.WriteAllLines("excluded.txt", new[] {@"C:\Windows"});
                _excluded = new[] {@"C:\Windows"};
            }
        }
        finally
        {
            stp.Stop();
            Console.WriteLine($"Excluded directory list loaded. ({Math.Round(stp.ElapsedMilliseconds / 1000.0D, 2)}s)"
                .Pastel(LimeGreen));
        }
    }

    private static IList<Task<List<TreeNode>>> CompileDirsAndSizes(string[] dirs)
    {
        var chunks = dirs.ChunkBy(2000);
        var tasks = chunks.Select(chunk => Task.Run(() =>
            {
                var nodes = chunk.Select(dir => TreeNodeExtensions.CreateNodeFromPath(dir, GetSizeOfDirectory(dir)))
                    .ToList();

                return nodes;
            }))
            .ToList();

        return tasks;
    }

    private static List<Task<List<string>>> GetDirectories(string path, int depth)
    {
        var dirs = Directory.GetDirectories(path + @"\")
            .Where(d => !_excluded.Any(d.StartsWith) || IsSymbolicLink(path) || IsHardLink(path)).ToList();
        dirs.Add(path);

        return dirs.Select(dir => Task.Run(() =>
        {
            return GetSubDirectories(dir, depth).Where(d => !_excluded.Any(d.StartsWith)).ToList();
        })).ToList();
    }

    private static IEnumerable<string> GetSubDirectories(string path, int depth)
    {
        path += "\\";
        try
        {
            if (_excluded.Any(path.StartsWith)) return new List<string>();
            if (IsSymbolicLink(path) || IsHardLink(path)) return new List<string>();

            var resultDirectories = new List<string>();
            var directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
            {
                //Console.WriteLine(directory);
                resultDirectories.Add(directory);
                if (depth > 0) resultDirectories.AddRange(GetSubDirectories(directory, depth - 1));
            }

            return resultDirectories;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"Unauthorized Access (Not included): {path}".Pastel(Red));
            _excluded = _excluded.Append(path).ToArray();
            return new List<string>();
        }
        catch (DirectoryNotFoundException)
        {
            Console.WriteLine($"Could not find a part of the path (Not included): {path}".Pastel(Red));
            return new List<string>();
        }
        catch (AggregateException e)
        {
            Console.WriteLine($"Aggregate Exception (Not included): {path} ({e})".Pastel(Red));
            return new List<string>();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception (Not included): {path} ({e})".Pastel(Red));
            return new List<string>();
        }
    }

    private static long GetSizeOfDirectory(string path)
    {
        /*var files = Directory.GetFiles(path);
        return files.Select(file => new FileInfo(file)).Select(fileInfo => fileInfo.Length).Sum();*/
        try
        {
            //check if path is empty
            if (Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0) return 0;

            return Directory.GetFiles(path).Select(file => new FileInfo(file)).Select(fileInfo => fileInfo.Length)
                .Sum();
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"Unauthorized Access (Not included): {path}".Pastel(Red));
            return 0;
        }
        catch (DirectoryNotFoundException)
        {
            Console.WriteLine($"Could not find a part of the path (Not included): {path}".Pastel(Red));
            return 0;
        }
        catch (AggregateException e)
        {
            Console.WriteLine($"Aggregate Exception (Not included): {path} ({e})".Pastel(Red));
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception (Not included): {path} ({e})".Pastel(Red));
            return 0;
        }
    }

    private static void PrintHelp(string msg = "")
    {
        Console.WriteLine(msg);
        Console.WriteLine("Usage: StorageSizeAnalysis.exe <path> <depth> <print depth (0 = unlimited)>");
        Console.WriteLine(@"Example: StorageSizeAnalysis.exe ""C:\Games"" 2 0");
        Environment.Exit(1);
    }

    private static string BytesToString(this long byteCount)
    {
        string[] suf = {"B", "KB", "MB", "GB", "TB", "PB", "EB"}; //Longs run out around EB
        if (byteCount == 0)
            return "0" + suf[0];
        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return Math.Sign(byteCount) * num + suf[place];
    }

    private static IEnumerable<List<T>> ChunkBy<T>(this IEnumerable<T> source, int chunkSize)
    {
        while (source.Any())
        {
            yield return source.Take(chunkSize).ToList();
            source = source.Skip(chunkSize);
        }
    }
}