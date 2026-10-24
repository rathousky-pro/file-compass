using System;
using System.IO;

namespace FileCompass
{
    // All operations deliberately fail on collisions; never overwrite user data.
    public static class FileOperations
    {
        public static string Destination(string source, string targetDirectory)
        {
            string fullSource = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
            string target = Path.GetFullPath(Path.Combine(targetDirectory, Path.GetFileName(fullSource)));
            if (String.Equals(fullSource, target, StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.Text("fileOperation.sameSourceAndTarget"));
            if (Directory.Exists(source) && target.StartsWith(fullSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException(L.Text("fileOperation.destinationInsideSource"));
            if (File.Exists(target) || Directory.Exists(target))
                throw new IOException(L.Text("fileOperation.existingDestinationPrefix") + target);
            return target;
        }

        public static void Copy(string source, string targetDirectory)
        {
            string target = Destination(source, targetDirectory);
            if (Directory.Exists(source)) CopyDirectory(source, target);
            else CopyFile(source, target);
        }

        private static void CopyFile(string source, string target)
        {
            RejectLink(source);
            bool created = false;
            try
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    input.CopyTo(output);
                    output.Flush(true);
                }
                File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
                AttachmentSecurity.WriteZone(target, AttachmentSecurity.ReadZone(source));
            }
            catch
            {
                // Remove only the new file owned by this operation, never an existing target.
                if (created) { try { File.Delete(target); } catch { } }
                throw;
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            RejectLink(source);
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException(L.Text("fileOperation.targetExistsPrefix") + target);
            Directory.CreateDirectory(target);
            // On failure retain completed files and explain partial results in the UI.
            foreach (string directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
            foreach (string file in Directory.GetFiles(source))
                CopyFile(file, Path.Combine(target, Path.GetFileName(file)));
        }

        public static void Move(string source, string targetDirectory)
        {
            RejectLink(source);
            string target = Destination(source, targetDirectory);
            // Native moves only. No copy/delete fallback across volumes in this first version.
            if (Directory.Exists(source)) Directory.Move(source, target);
            else File.Move(source, target);
        }

        public static void Rename(string source, string name)
        {
            ValidateName(name);
            string target = Path.Combine(Path.GetDirectoryName(source), name);
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException(L.Text("fileOperation.nameExists"));
            if (Directory.Exists(source)) Directory.Move(source, target);
            else File.Move(source, target);
        }

        public static void ValidateName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name == "." || name == ".." ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") || name.EndsWith(" "))
                throw new IOException(L.Text("fileOperation.validNameRequired"));
            string stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
                System.Text.RegularExpressions.Regex.IsMatch(stem, "^(COM|LPT)[1-9]$"))
                throw new IOException(L.Text("fileOperation.reservedWindowsName"));
        }

        internal static void RejectLink(string path)
        {
            PathSafety.RejectLinks(path);
        }
    }
}

