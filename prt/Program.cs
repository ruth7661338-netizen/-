using System.CommandLine;
using System.Text;


var languageExtensions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["cs"] = new[] { ".cs" },
    ["csharp"] = new[] { ".cs" },
    ["java"] = new[] { ".java" },
    ["python"] = new[] { ".py" },
    ["py"] = new[] { ".py" },
    ["js"] = new[] { ".js", ".jsx" },
    ["javascript"] = new[] { ".js", ".jsx" },
    ["ts"] = new[] { ".ts", ".tsx" },
    ["typescript"] = new[] { ".ts", ".tsx" },
    ["html"] = new[] { ".html", ".htm" },
    ["css"] = new[] { ".css", ".scss" },
    ["sql"] = new[] { ".sql" },
    ["c"] = new[] { ".c", ".h" },
    ["cpp"] = new[] { ".cpp", ".hpp", ".h" },
};


string[] excludedFolders = { "bin", "obj", "debug", "release", "node_modules", ".git", ".vs", ".vscode", ".idea" };


Option<string[]> languageOption = new("--language", "-l")
{
    Description = "Programming languages to include (e.g. cs js), or 'all' for every code file",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

Option<FileInfo> outputOption = new("--output", "-o")
{
    Description = "Name of the bundle file, or a full path",
    DefaultValueFactory = _ => new FileInfo("bundle.txt")
};

Option<bool> noteOption = new("--note", "-n")
{
    Description = "Write the source file name and relative path as a comment"
};

Option<string> sortOption = new("--sort", "-s")
{
    Description = "Order of files: 'name' (alphabetical, default) or 'type' (by code type)",
    DefaultValueFactory = _ => "name"
};
sortOption.AcceptOnlyFromAmong("name", "type");

Option<bool> removeEmptyLinesOption = new("--remove-empty-lines", "-r")
{
    Description = "Remove empty lines from the source code"
};

Option<string> authorOption = new("--author", "-a")
{
    Description = "Name of the bundle file creator"
};


Command bundleCommand = new("bundle", "Bundle code files into a single file");
bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(sortOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);

bundleCommand.SetAction(parseResult =>
{
    string[] languages = parseResult.GetValue(languageOption) ?? Array.Empty<string>();
    FileInfo output = parseResult.GetValue(outputOption)!;
    bool note = parseResult.GetValue(noteOption);
    string sort = parseResult.GetValue(sortOption) ?? "name";
    bool removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
    string? author = parseResult.GetValue(authorOption);

    string currentDir = Directory.GetCurrentDirectory();


    HashSet<string> extensions;
    if (languages.Any(l => l.Equals("all", StringComparison.OrdinalIgnoreCase)))
    {
        extensions = languageExtensions.Values.SelectMany(e => e).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    else
    {
        extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lang in languages)
        {
            if (languageExtensions.TryGetValue(lang, out var exts))
                extensions.UnionWith(exts);
            else
                Console.WriteLine($"Warning: unknown language '{lang}' was ignored");
        }
        if (extensions.Count == 0)
        {
            Console.WriteLine("Error: no valid language was given");
            return 1;
        }
    }


    var files = Directory.EnumerateFiles(currentDir, "*.*", SearchOption.AllDirectories)
        .Where(f => extensions.Contains(Path.GetExtension(f)))
        .Where(f => !Path.GetRelativePath(currentDir, f)
                        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Any(part => excludedFolders.Contains(part, StringComparer.OrdinalIgnoreCase)))
        .Where(f => !string.Equals(Path.GetFullPath(f), output.FullName, StringComparison.OrdinalIgnoreCase));


    files = sort == "type"
        ? files.OrderBy(f => Path.GetExtension(f)).ThenBy(f => Path.GetFileName(f))
        : files.OrderBy(f => Path.GetFileName(f));

    var fileList = files.ToList();
    if (fileList.Count == 0)
    {
        Console.WriteLine("No matching code files were found");
        return 0;
    }


    try
    {
        using var writer = new StreamWriter(output.FullName, false, Encoding.UTF8);

        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine($"// Author: {author}");
            writer.WriteLine();
        }

        foreach (var file in fileList)
        {
            if (note)
            {
                writer.WriteLine($"// Source: {Path.GetFileName(file)} | Path: {Path.GetRelativePath(currentDir, file)}");
            }

            var lines = File.ReadAllLines(file);
            if (removeEmptyLines)
                lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

            foreach (var line in lines)
                writer.WriteLine(line);

            writer.WriteLine();
        }

        Console.WriteLine($"Bundled {fileList.Count} files into {output.FullName}");
        return 0;
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine($"Error: the folder of '{output.FullName}' does not exist");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine($"Error: no permission to write to '{output.FullName}'");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"Error: could not save the file - {ex.Message}");
    }
    return 1;
});



Command createRspCommand = new("create-rsp", "Create a response file with a ready bundle command");

createRspCommand.SetAction(parseResult =>
{
    var command = new StringBuilder("bundle");


    string languages;
    do
    {
        Console.Write("Languages to include (e.g. cs js, or all): ");
        languages = Console.ReadLine()?.Trim() ?? "";
        if (languages.Length == 0)
            Console.WriteLine("This option is required.");
    } while (languages.Length == 0);
    command.Append($" --language {languages}");

    Console.Write("Output file name or path (Enter for bundle.txt): ");
    string output = Console.ReadLine()?.Trim() ?? "";
    if (output.Length > 0)
        command.Append($" --output \"{output}\"");

    if (AskYesNo("Add source file name and path as a comment? (y/n): "))
        command.Append(" --note");

    Console.Write("Sort by 'name' or 'type' (Enter for name): ");
    string sort = Console.ReadLine()?.Trim().ToLower() ?? "";
    if (sort == "type")
        command.Append(" --sort type");

    if (AskYesNo("Remove empty lines? (y/n): "))
        command.Append(" --remove-empty-lines");

    Console.Write("Author name (Enter to skip): ");
    string author = Console.ReadLine()?.Trim() ?? "";
    if (author.Length > 0)
        command.Append($" --author \"{author}\"");

    Console.Write("Response file name (Enter for bundle.rsp): ");
    string rspName = Console.ReadLine()?.Trim() ?? "";
    if (rspName.Length == 0) rspName = "bundle.rsp";
    if (!rspName.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase)) rspName += ".rsp";

    try
    {
        File.WriteAllText(rspName, command.ToString());
        Console.WriteLine($"Response file created: {Path.GetFullPath(rspName)}");
        Console.WriteLine($"Run it with:  fib @{rspName}");
        return 0;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.WriteLine($"Error: could not create the response file - {ex.Message}");
        return 1;
    }
});


RootCommand rootCommand = new("File Bundler CLI");
rootCommand.Subcommands.Add(bundleCommand);
rootCommand.Subcommands.Add(createRspCommand);

return rootCommand.Parse(args).Invoke();


static bool AskYesNo(string question)
{
    Console.Write(question);
    string answer = Console.ReadLine()?.Trim().ToLower() ?? "";
    return answer == "y" || answer == "yes";
}