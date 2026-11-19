using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    /// <summary>
    /// For the tools run with -executeMethod: reading their arguments, and finishing the run. Each editor assembly has its
    /// own (this one's tools can't reference the other's).
    /// </summary>
    internal static class CommandLine
    {
        /// <summary>The value given after name on Unity's command line (-scene [path], say), or null if it isn't there.</summary>
        public static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>Whether name (-force, say) is on Unity's command line.</summary>
        public static bool Has(string name) => Environment.GetCommandLineArgs().Contains(name);

        /// <summary>
        /// Ends a command-line run: in batch mode, Unity exits, with code 0 if it went well and 1 if not, so a batch-mode
        /// Unity started without -quit doesn't stay open, idle, once the tool is done, and a failure shows in its exit
        /// code. Outside batch mode (the method called from a script in an open editor), the editor stays open.
        /// </summary>
        public static void Finish(bool succeeded)
        {
            if (Application.isBatchMode) { EditorApplication.Exit(succeeded ? 0 : 1); }
        }
    }
}
