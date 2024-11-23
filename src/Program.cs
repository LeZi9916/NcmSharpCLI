using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
namespace NcmSharp.CLI
{
    internal class Program
    {
        static string _workingPath = Directory.GetCurrentDirectory();
        static string _outputPath = Path.Combine(_workingPath, "unlock");
        static long _fileCount = 0;
        static long _successCount = 0;
        static long _failureCount = 0;
        static bool _ignoreExtension = false;
        static bool _useMemoryAsCache = false;
        static async Task Main(string[] args)
        {
            var @params = ReadArgs(args);
            var isChecked = false;
            var threadCount = 1;
            var allowMultiThread = false;
            var ncmPaths = new Queue<FileInfo>();
            
            foreach (var param in @params)
            {
                var value = param.Value;
                switch (param.Prefix)
                {
                    case "-i":
                    case "--igonre-extension":
                        _ignoreExtension = true;
                        break;
                    case "-p":
                    case "--working-path":
                        if (string.IsNullOrEmpty(value) || !Directory.Exists(value))
                        {
                            Console.WriteLine("Error: Invliad directory path");
                            Environment.Exit(-127);
                        }
                        _workingPath = value;
                        break;
                    case "-o":
                    case "--output-path":
                        var isValid = false;
                        _outputPath = value;
                        try
                        {
                            if (!Directory.Exists(value))
                            {
                                Directory.CreateDirectory(value);
                            }
                            isValid = true;
                        }
                        catch { }
                        if (!isValid || string.IsNullOrEmpty(value))
                        {
                            Console.WriteLine("Error: Invalid directory path");
                            Environment.Exit(-127);
                        }
                        isChecked = true;
                        break;
                    case "-j":
                    case "--jobs":
                        if (!int.TryParse(value, out threadCount) || threadCount < 1)
                        {
                            Console.WriteLine("Error: The number of threads must be Int32 and greater than 0");
                            Environment.Exit(-127);
                        }
                        allowMultiThread = true;
                        break;
                    case "-c":
                    case "--use-memory-as-cache":
                        _useMemoryAsCache = true;
                        break;
                    case "-h":
                    case "--help":
                        Console.WriteLine(
                            $"""
                            NcmSharp CLI v{Assembly.GetExecutingAssembly().GetName().Version}
                            Usage:
                                NcmSharpCLI [options]

                            NcmSharpCLI is a software for decrypting ncm format encrypted audio. 
                            By default, it will search for all files with the ".ncm" suffix in the current directory and try to decrypt them.

                            Options:
                                -p,--working-path <path>    Set the directory for storing ncm encrypted audio files
                                -o,--output-path  <path>    Set the output directory
                                -j,--jobs         <count>   Set the maximum decryption workflow
                                                            WARNNING: This option is used to specify the maximum asynchronous workflow limit, 
                                                            which does not necessarily use all threads.
                                -c,--use-memory-as-cache    Read the file and store it in the memory buffer before decrypting it.
                                -i,--igonre-extension       Ignore file extensions and attempt to decrypt all files
                                -h,--help                   print help and exit
                            """);
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine($"NcmSharpCLI: invalid option -- \"{param.Prefix}\"");
                        goto case "-h";
                }
            }
            foreach (var path in Directory.GetFiles(_workingPath))
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Extension != ".ncm" && !_ignoreExtension)
                    continue;
                ncmPaths.Enqueue(fileInfo);
                _fileCount++;
            }
            if(ncmPaths.Count == 0)
            {
                Console.WriteLine("Nothing to do");
                Environment.Exit(0);
            }
            if (!isChecked)
            {
                if (!Directory.Exists(_outputPath))
                    Directory.CreateDirectory(_outputPath);
                isChecked = true;
            }
            var tasks = new Task[threadCount];
            var startAt = DateTime.Now;
            if (!allowMultiThread)
            {
                while (ncmPaths.Count > 0)
                {
                    var fileInfo = ncmPaths.Dequeue();
                    await DumpAsync(fileInfo);
                }
            }
            else
            {
                await Task.Run(async () =>
                {
                    while (ncmPaths.Count > 0)
                    {
                        for (var i = 0; i < tasks.Length; i++)
                        {
                            ref var task = ref tasks[i];
                            if (task is null || task.IsCompleted)
                            {
                                var fileInfo = ncmPaths.Dequeue();
                                task = DumpAsync(fileInfo);
                                break;
                            }
                        }
                        await Task.Delay(5);
                    }
                });
                await Task.WhenAll(tasks.Where(x => x != null));
            }
            var endAt = DateTime.Now;
            var timeSpan = endAt - startAt;
            Console.WriteLine(
                $"""
                [Finished] All files have been converted
                    Success:{_successCount}
                    Failure:{_failureCount}
                    Total  :{_fileCount}
                    Elapsed:{timeSpan}
                """);

        }
        static async Task DumpAsync(FileInfo fileInfo)
        {
            try
            {
                var ncmFileInfo = await NcmHelper.ReadFileAsync(fileInfo.FullName);
                var meta = ncmFileInfo.Meta;
                var artist = meta.Artist?.FirstOrDefault()?.FirstOrDefault() ?? "Undefined";
                var filename = $"{meta.MusicName} - {artist}";
                var invalidChar = Path.GetInvalidFileNameChars();
                foreach (var c in invalidChar)
                {
                    if (filename.Contains(c))
                        filename = filename.Replace(c, '_');
                }
                var outputPath = Path.Combine(_outputPath, $"{filename}.{meta.Format}");
                if (File.Exists(outputPath))
                {
                    for (var i = 1; i < 99; i++)
                    {
                        var newFilename = $"{filename}_{i}.{meta.Format}";
                        var newPath = Path.Combine(_outputPath, newFilename);
                        if (!File.Exists(newPath))
                        {
                            outputPath = newPath;
                            break;
                        }
                    }
                }
                if(_useMemoryAsCache)
                {
                    var ncmStruct = await ncmFileInfo.DumpAsStructAsync();
                    await ncmStruct.DumpAsync(outputPath);
                }
                else
                {
                    await ncmFileInfo.DumpAsFileAsync(outputPath);
                }
                Console.WriteLine($"[Success] {meta.MusicName}.{meta.Format}");
                _successCount++;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] An error occurred while decrypting {fileInfo.Name}");
                _failureCount++;
            }
        }
        static ConsoleParam[] ReadArgs(string[] args)
        {
            Span<ConsoleParam> consoleParams = new ConsoleParam[args.Length];
            var index = 0;
            for (var i = 0; i < args.Length; i++)
            {
                var isLast = i == args.Length - 1;
                var content = args[i];
                if (!IsValidPrefix(content))
                    continue;
                var prefix = content;
                var value = !isLast && !IsValidPrefix(args[i + 1]) ? args[i + 1] : string.Empty;
                if (string.IsNullOrEmpty(value))
                    i++;
                consoleParams[index++] = new ConsoleParam()
                {
                    Prefix = prefix,
                    Value = value
                };
            }
            return consoleParams.Slice(0, index).ToArray();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsValidPrefix(string? prefix)
        {
            return !string.IsNullOrEmpty(prefix) && prefix!.StartsWith("-");
        }
        readonly struct ConsoleParam
        {
            public required string Prefix { get; init; }
            public required string Value { get; init; }
        }
    }
}
