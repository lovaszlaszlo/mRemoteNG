using System;
using System.IO;
using System.Runtime.Versioning;
using mRemoteNG.App;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using mRemoteNG.Tools;

namespace mRemoteNG.Config.DataProviders
{
    public class FileBackupCreator
    {
        [SupportedOSPlatform("windows")]
        public void CreateBackupFile(string fileName)
        {
            try
            {
                if (WeDontNeedToBackup(fileName))
                    return;

                PathValidator.ValidatePathOrThrow(fileName, nameof(fileName));

                DateTime takenAt = DateTime.Now;

                // Asks BackupPath rather than formatting the name here, so the pruner looks for
                // exactly the files this writes - in the folder the user configured.
                string backupFileName = Path.Combine(
                    BackupPath.DirectoryFor(fileName),
                    BackupPath.BackupFileNameFor(fileName, takenAt));

                PathValidator.ValidatePathOrThrow(backupFileName, nameof(backupFileName));

                File.Copy(fileName, backupFileName);

                // File.Copy carries the source timestamp over, and the source is the state from
                // the *previous* save - so a backup folder sorted by date showed the newest
                // backup as the oldest file. Stamp it with when the backup was taken, which is
                // what the name says and what anyone reading the folder means by the date.
                File.SetLastWriteTime(backupFileName, takenAt);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.ConnectionsFileBackupFailed, ex,
                                                             MessageClass.WarningMsg);
                throw;
            }
        }

        private bool WeDontNeedToBackup(string filePath)
        {
            return FeatureIsTurnedOff() || FileDoesntExist(filePath);
        }

        private bool FileDoesntExist(string filePath)
        {
            return !File.Exists(filePath);
        }

        private bool FeatureIsTurnedOff()
        {
            return Properties.OptionsBackupPage.Default.BackupFileKeepCount == 0;
        }
    }
}