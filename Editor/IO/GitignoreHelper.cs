using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EditorThemeKit
{
    /// <summary>
    /// Offers, once per project, to add the generated-stylesheet folder to the project's
    /// <c>.gitignore</c> (it's a per-user build artifact and shouldn't be committed).
    /// </summary>
    internal static class GitignoreHelper
    {
        private const string Pattern = "Assets/EditorThemeKit.Generated/";

        // EditorPrefs are global to the editor install, so key by project path to keep the
        // "asked already" flag per-project.
        private static string PromptKey => "EditorThemeKit.GitignorePrompted:" + Application.dataPath;

        /// <summary>
        /// Prompts (once) to add the generated folder to <c>.gitignore</c>. No-ops if already
        /// asked, already ignored, or the project isn't under git.
        /// </summary>
        public static void MaybePromptOnce()
        {
            try
            {
                if (EditorPrefs.GetBool(PromptKey, false))
                    return;

                var root = Directory.GetParent(Application.dataPath)!.FullName;
                var gitignore = Path.Combine(root, ".gitignore");
                bool hasGit = Directory.Exists(Path.Combine(root, ".git"));
                bool hasGitignore = File.Exists(gitignore);

                if (!hasGit && !hasGitignore)
                    return; // not a git project — nothing to do

                if (hasGitignore && File.ReadAllText(gitignore).Contains(Pattern))
                {
                    EditorPrefs.SetBool(PromptKey, true); // already ignored
                    return;
                }

                // Defer the dialog off the current callstack (e.g. asset import / startup).
                EditorApplication.delayCall += () =>
                {
                    if (EditorPrefs.GetBool(PromptKey, false))
                        return;
                    EditorPrefs.SetBool(PromptKey, true);

                    bool add = EditorUtility.DisplayDialog(
                        "Editor Theme Kit",
                        "Editor Theme Kit writes a generated stylesheet to:\n\n" +
                        "    Assets/EditorThemeKit.Generated/\n\n" +
                        "It's a per-user build artifact and normally shouldn't be committed. " +
                        "Add it to your .gitignore?",
                        "Add to .gitignore", "Not now");

                    if (add)
                        AppendPattern(gitignore);
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] .gitignore check failed: {e.Message}");
            }
        }

        private static void AppendPattern(string gitignorePath)
        {
            try
            {
                string content = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
                if (content.Contains(Pattern))
                    return;

                if (content.Length > 0 && !content.EndsWith("\n") && !content.EndsWith("\r\n"))
                    content += Environment.NewLine;

                content += Environment.NewLine
                         + "# Editor Theme Kit (generated stylesheet — per-user build artifact)"
                         + Environment.NewLine + Pattern + Environment.NewLine;

                File.WriteAllText(gitignorePath, content);
                Debug.Log($"[Editor Theme Kit] Added '{Pattern}' to .gitignore.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Editor Theme Kit] Failed to update .gitignore: {e.Message}");
            }
        }
    }
}
