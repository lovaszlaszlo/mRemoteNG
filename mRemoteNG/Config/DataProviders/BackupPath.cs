using System.IO;
using System.Runtime.Versioning;
using mRemoteNG.App;
using mRemoteNG.Messages;

namespace mRemoteNG.Config.DataProviders
{
    /// <summary>
    /// Decides which folder the rolling backups of a settings file go into, and what they are
    /// called.
    /// </summary>
    /// <remarks>
    /// The backup location on the options page had no effect at all: nothing read
    /// <see cref="Properties.OptionsBackupPage.BackupLocation"/>, so backups always landed next to
    /// the file they copied, whatever the user typed there.
    ///
    /// <see cref="FileBackupCreator"/> and <see cref="FileBackupPruner"/> both ask here so they
    /// cannot disagree about the folder - and they did disagree about the name: the creator passed
    /// the full path as {0} of the name format while the pruner passed the bare file name. Both go
    /// through <see cref="BackupFileNameFor"/> now, which uses the bare name, so the two always
    /// describe the same file.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal static class BackupPath
    {
        private static bool _missingFolderReported;

        /// <summary>
        /// The folder to put backups of <paramref name="filePath"/> in.
        /// </summary>
        /// <remarks>
        /// The configured location is only honoured when it names a folder that exists. That check
        /// is not just defensiveness: <c>ConnectionsService.UpdateCustomConsPathSetting</c> and the
        /// <c>-cons</c> command line switch both write a *file* path into the same setting. Those
        /// values fail the check and fall back to the old behaviour instead of scattering backups
        /// somewhere unexpected.
        /// </remarks>
        public static string DirectoryFor(string filePath)
        {
            // Empty rather than null when the path carries no folder of its own, which leaves
            // Path.Combine returning the bare name - the behaviour there was before.
            string ownFolder = Path.GetDirectoryName(filePath) ?? string.Empty;
            string configured = Properties.OptionsBackupPage.Default.BackupLocation?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(configured))
                return ownFolder;

            if (Directory.Exists(configured))
                return configured;

            // A save happens after every edit, so complaining every time would bury the
            // notifications pane. Once per run is enough to explain why the setting is not taking.
            if (!_missingFolderReported)
            {
                _missingFolderReported = true;
                Runtime.MessageCollector?.AddMessage(MessageClass.WarningMsg,
                    $"The configured backup location is not an existing folder, so backups are " +
                    $"being written next to the file instead: {configured}");
            }

            return ownFolder;
        }

        /// <summary>
        /// The name - not the path - of a backup of <paramref name="filePath"/>.
        /// </summary>
        /// <param name="filePath">The file being backed up.</param>
        /// <param name="timestampOrWildcard">
        /// A <see cref="System.DateTime"/> to name one backup, or "*" to build a search pattern
        /// that matches all of them.
        /// </param>
        public static string BackupFileNameFor(string filePath, object timestampOrWildcard)
        {
            return string.Format(Properties.OptionsBackupPage.Default.BackupFileNameFormat,
                                 Path.GetFileName(filePath), timestampOrWildcard);
        }
    }
}
