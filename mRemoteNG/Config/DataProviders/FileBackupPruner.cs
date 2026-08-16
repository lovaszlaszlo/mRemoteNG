using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using mRemoteNG.Tools;

namespace mRemoteNG.Config.DataProviders
{
    public class FileBackupPruner
    {
        // Matches FileBackupCreator. Everything it reaches - the path validator, the backup
        // location - is Windows only, and it is only ever called from frmMain.
        [SupportedOSPlatform("windows")]
        public void PruneBackupFiles(string filePath, int maxBackupsToKeep)
        {
            PathValidator.ValidatePathOrThrow(filePath, nameof(filePath));

            string fileName = Path.GetFileName(filePath);

            // The same folder the creator wrote into, which is not necessarily the one the file
            // itself lives in.
            string directoryName = BackupPath.DirectoryFor(filePath);

            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(directoryName))
                return;

            string searchPattern = BackupPath.BackupFileNameFor(filePath, "*");
            string[] files = Directory.GetFiles(directoryName, searchPattern);

            if (files.Length <= maxBackupsToKeep)
                return;

            System.Collections.Generic.IEnumerable<string> filesToDelete = files
                                .OrderByDescending(s => s)
                                .Skip(maxBackupsToKeep);

            foreach (string file in filesToDelete)
            {
                File.Delete(file);
            }
        }
    }
}