using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using SmartCommandLineParser;

namespace multirun
{
    class Program
    {
	    static bool verbose;
	    static string baseDirPath;
        
        static void Main(string[] args)
	    {
		    var options = new CommandLineOptions();
		    options.AddRequired<string>("dirOrFile", help:"Path to directory to file search or a path to the text file contains file paths (one per line).");
		    options.AddOptional("verbose", false, new[] { "-v", "--verbose" }, help:"Show command to run.");
		    options.AddOptional("filter", "", new[] { "-f", "--filter" }, help:"File path filter (regular expression like '[.](hx|html)$' or file name pattern like '*.hx;*.html').");
		    options.AddOptional("checkPath", "", new[] { "-u", "--update" }, help:"Update files in <baseDirPath>.\n"
                                                                                + "Command will run only if <checkPath> does not exists or older than the file in <baseDirPath>.\n"
                                                                                + "Use special sequences in <checkPath> (see below)."
		    );
		    options.AddRequired<string>("command", help:"Command to run.");
            options.AddRepeatable<string>("commandArgs", help:"Command's arguments. Use special sequences to specify file path parts:\n"
                                                            + "$P - full path to file\n"
                                                            + "$D - path to directory\n"
                                                            + "$R - relative path to directory\n"
                                                            + "$F - file name with extension\n"
                                                            + "$N - file name w/o extension\n"
                                                            + "$E - file name extension (for example: '.txt')"
		    );
		    
		    if (args.Length > 0)
		    {
                options.Parse(args.TakeWhile(x => x != "--"));
			    
			    var dirOrFile = options.Get<string>("dirOrFile");
			    
                verbose = options.Get<bool>("verbose");
                var filter = options.Get<string>("filter");
			    var checkPath = options.Get<string>("checkPath");
			    
                var command = options.Get<string>("command");
                var commandArgs = options.Get<List<string>>("commandArgs").Concat(args.SkipWhile(x => x != "--").Skip(1));
			    
			    if (dirOrFile == "") error("Argument <dirOrFile> must be specified.");
			    if (command == "") error("Argument <command> must be specified.");
			    
			    if (filter == "*")
			    {
				    filter = "";
			    }
			    else
			    if (new Regex("\\*\\.[a-zA-Z0-9_?*]+(?:;\\*\\.[a-zA-Z0-9_?*]+)*").IsMatch(filter))
			    {
				    filter = string.Join("|", filter.Split(';').Select(s => "^" + s.Trim().Replace(".", "\\.").Replace("*", ".*").Replace("?", ".") + "$"));
			    }
			    
			    if (Directory.Exists(dirOrFile))
			    {
                    baseDirPath = dirOrFile;
					processDir(dirOrFile, new Regex(filter, RegexOptions.IgnoreCase), checkPath != "" ? checkPath : null, command, commandArgs.ToArray());
				}
				else if (File.Exists(dirOrFile))
                {
                    baseDirPath = "";
					processList(dirOrFile, new Regex(filter, RegexOptions.IgnoreCase), checkPath != "" ? checkPath : null, command, commandArgs.ToArray());
				}
			    else
			    {
				    error("Directory or file '" + dirOrFile + "' is not found.");
			    }
            }
		    else
		    {
			    help(options);
		    }
	    }
	    
	    static void error(string message)
	    {
		    if (!string.IsNullOrEmpty(message))
		    {
			    Console.WriteLine("Error: " + message + ".");
		    }
		    
		    Environment.Exit(1);
	    }
	    
	    static void help(CommandLineOptions options)
	    {
		    Console.WriteLine("Multirun is a tool to run specified command for list of files.");
		    Console.WriteLine("Multirun can search for files in specified directory or read list of them from specified text file. Usage:");
		    Console.WriteLine("");
		    Console.WriteLine("        multirun <baseDirPath> [ <arguments> ] <command> -- <commandArgs>");
		    Console.WriteLine("");
		    Console.WriteLine("Arguments:");
		    Console.WriteLine(options.GetHelpMessage());
		    Console.WriteLine("");
		    Console.WriteLine("Examples ('--' is necessary only if you want to specify custom switches to command):\n");
		    Console.WriteLine("\tmultirun c:\\test -f *.txt notepad.exe $F");
		    Console.WriteLine("\tmultirun c:\\test myProg.exe -- -MyProgSwitch $F");
	    }
	    
	    static void processDir(string dirPath, Regex reFiles, string checkPath, string command, string[] commandArgs)
	    {
		    dirPath = dirPath != "" ? dirPath.Replace("/", "\\").TrimEnd('\\') : ".";
		    
		    foreach (var path in Directory.EnumerateDirectories(dirPath))
		    {
				processDir(path, reFiles, checkPath, command, commandArgs);
		    }
		    foreach (var path in Directory.EnumerateFiles(dirPath))
		    {
				processFile(path, reFiles, checkPath, command, commandArgs);
		    }
	    }
	    
	    static void processList(string listFilePath, Regex reFiles, string checkPath, string command, string[] commandArgs)
	    {
		    foreach (var path in File.ReadAllText(listFilePath).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
		    {
			    if (path.Trim() != "")
			    {
				    processFile(path.Trim(), reFiles, checkPath, command, commandArgs);
			    }
		    }
	    }
	    
	    static void processFile(string path, Regex reFiles, string checkPath, string command, string[] commandArgs)
	    {
		    if (reFiles.IsMatch(path))
		    {
			    var realCheckPath = checkPath == null ? null : applySubs(checkPath, path);
			    
			    if (realCheckPath == null
			     || !File.Exists(realCheckPath)
			     || File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(realCheckPath)
			    ) {
				    var args = commandArgs.Select(arg => applySubs(arg, path)).ToArray();
				    if (verbose) Console.WriteLine("[run] " + command + " " + string.Join(" ", args.Select(encodeParameter)));
				    
				    Process.Start(command, string.Join(" ", args.Select(encodeParameter)));
			    }
		    }
	    }
	    
	    static string applySubs(string s, string path)
	    {
            var R = Path.GetDirectoryName(path).Substring(baseDirPath.Length);
		    if (R == "") R = ".";
		    
		    var E = Path.GetExtension(path);
		    
		    path = path.Replace("/", "\\");
		    s = s.Replace("$P", path);
		    s = s.Replace("$D", Path.GetDirectoryName(path));
		    s = s.Replace("$R", R);
		    s = s.Replace("$F", Path.GetFileName(path));
		    s = s.Replace("$N", Path.GetFileNameWithoutExtension(path));
		    s = s.Replace("$E", E);
		    return s;
	    }
	    
        private static string encodeParameter(string argument)
        {
            if (argument == null) throw new ArgumentNullException(nameof(argument));
            if (argument.Length > 0 && argument.IndexOfAny(" \t\n\v\"".ToCharArray()) == -1) return argument;
            return $"\"{argument}\"";
        }
    }
}
