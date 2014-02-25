using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.IO
{
    internal static class Path2
    {

        internal const int MAX_PATH = 260;

        internal const int MAX_DIRECTORY_PATH = 248;

        /// <summary>Provides a platform-specific character used to separate directory levels in a path string that reflects a hierarchical file system organization.</summary>
        /// <filterpriority>1</filterpriority>
        public readonly static char DirectorySeparatorChar;

        /// <summary>Provides a platform-specific alternate character used to separate directory levels in a path string that reflects a hierarchical file system organization.</summary>
        /// <filterpriority>1</filterpriority>
        public readonly static char AltDirectorySeparatorChar;

        /// <summary>Provides a platform-specific volume separator character.</summary>
        /// <filterpriority>1</filterpriority>
        public readonly static char VolumeSeparatorChar;

        /// <summary>Provides a platform-specific array of characters that cannot be specified in path string arguments passed to members of the <see cref="T:System.IO.Path" /> class.</summary>
        /// <returns>A character array of invalid path characters for the current platform.</returns>
        /// <filterpriority>1</filterpriority>
        [Obsolete("Please use GetInvalidPathChars or GetInvalidFileNameChars instead.")]
        public readonly static char[] InvalidPathChars;

        internal readonly static char[] TrimEndChars;

        private readonly static char[] RealInvalidPathChars;

        private readonly static char[] InvalidFileNameChars;

        /// <summary>A platform-specific separator character used to separate path strings in environment variables.</summary>
        /// <filterpriority>1</filterpriority>
        public readonly static char PathSeparator;

        internal readonly static int MaxPath;

        private readonly static int MaxDirectoryLength;

        internal readonly static int MaxLongPath;

        private readonly static string Prefix;

        private readonly static char[] s_Base32Char;

        static Path2()
        {
            Path2.DirectorySeparatorChar = '\\';
            Path2.AltDirectorySeparatorChar = '/';
            Path2.VolumeSeparatorChar = ':';
            char[] chrArray = new char[] { '\"', '<', '>', '|', '\0', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\a', '\b', '\t', '\n', '\v', '\f', '\r', '\u000E', '\u000F', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F' };
            Path2.InvalidPathChars = chrArray;
            char[] chrArray1 = new char[] { '\t', '\n', '\v', '\f', '\r', ' ', '\u0085', '\u00A0' };
            Path2.TrimEndChars = chrArray1;
            char[] chrArray2 = new char[] { '\"', '<', '>', '|', '\0', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\a', '\b', '\t', '\n', '\v', '\f', '\r', '\u000E', '\u000F', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F' };
            Path2.RealInvalidPathChars = chrArray2;
            char[] chrArray3 = new char[] { '\"', '<', '>', '|', '\0', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\a', '\b', '\t', '\n', '\v', '\f', '\r', '\u000E', '\u000F', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F', ':', '*', '?', '\\', '/' };
            Path2.InvalidFileNameChars = chrArray3;
            Path2.PathSeparator = ';';
            Path2.MaxPath = 260;
            Path2.MaxDirectoryLength = 255;
            Path2.MaxLongPath = 32000;
            Path2.Prefix = "\\\\?\\";
            char[] chrArray4 = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5' };
            Path2.s_Base32Char = chrArray4;
        }

        internal static void CheckInvalidPathChars(string path, bool checkAdditional = false)
        {
            if (path != null)
            {
                if (!Path2.HasIllegalCharacters(path, checkAdditional))
                {
                    return;
                }
                else
                {
                    throw new ArgumentException("The path has invalid characters.", "path");
                }
            }
            else
            {
                throw new ArgumentNullException("path");
            }
        }

        internal static bool HasIllegalCharacters(string path, bool checkAdditional)
        {
            int num = 0;
            while (num < path.Length)
            {
                int num1 = path[num];
                if (num1 == 34 || num1 == 60 || num1 == 62 || num1 == 124 || num1 < 32)
                {
                    return true;
                }
                else
                {
                    if (!checkAdditional || num1 != 63 && num1 != 42)
                    {
                        num++;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static string GetFileName(string path)
        {
            char chr;
            if (path != null)
            {
                Path2.CheckInvalidPathChars(path, false);
                int length = path.Length;
                int num = length;
                do
                {
                    int num1 = num - 1;
                    num = num1;
                    if (num1 < 0)
                    {
                        return path;
                    }
                    chr = path[num];
                }
                while (chr != Path2.DirectorySeparatorChar && chr != Path2.AltDirectorySeparatorChar && chr != Path2.VolumeSeparatorChar);
                return path.Substring(num + 1, length - num - 1);
            }
            return path;
        }
    }
}
