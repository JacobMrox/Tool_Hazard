using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard.Script.Compilation
{
    public interface IFileIncluder
    {
        string GetIncludePath(string currentScriptPath, string path);
        string? GetContent(string scriptPath);
    }

    public class SimpleFileIncluder : IFileIncluder
    {
        public string WorkingDirectory { get; }

        public SimpleFileIncluder() : this(Environment.CurrentDirectory)
        {
        }

        public SimpleFileIncluder(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
        }

        public string GetIncludePath(string currentScriptPath, string path)
        {
            if (Path.IsPathRooted(path))
            {
                return currentScriptPath;
            }

            // Check relative to script first
            var scriptDirectory = Path.GetDirectoryName(currentScriptPath);
            var relativePath = Path.Combine(scriptDirectory, path);
            if (File.Exists(relativePath))
            {
                return relativePath;
            }

            // Then working directory
            return Path.Combine(WorkingDirectory, path);
        }

        public string? GetContent(string scriptPath)
        {
            if (!File.Exists(scriptPath))
                return null;
            return File.ReadAllText(scriptPath);
        }
    }

    public class StringFileIncluder : IFileIncluder
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

        public StringFileIncluder()
        {
        }

        public StringFileIncluder(string path, string content)
        {
            _files[path] = content;
        }

        public void AddFile(string path, string content)
        {
            _files[path] = content;
        }

        public string GetIncludePath(string currentScriptPath, string path)
        {
            var slashIndex = currentScriptPath.LastIndexOfAny(new[] { '\\', '/' });
            if (slashIndex != -1)
            {
                var dir = currentScriptPath.Substring(0, slashIndex + 1);
                if (path[0] == '\\' || path[0] == '/')
                {
                    return dir + path.Substring(1);
                }
                return dir + path;
            }
            return path;
        }

        public string? GetContent(string scriptPath)
        {
            if (!_files.TryGetValue(scriptPath, out var content))
                return null;
            return content;
        }
    }
}
