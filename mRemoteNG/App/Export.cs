using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.Config.Connections;
using mRemoteNG.Config.DataProviders;
using mRemoteNG.Config.Serializers;
using mRemoteNG.Config.Serializers.ConnectionSerializers.Csv;
using mRemoteNG.Config.Serializers.ConnectionSerializers.Xml;
using mRemoteNG.Connection;
using mRemoteNG.Container;
using mRemoteNG.Resources.Language;
using mRemoteNG.Security;
using mRemoteNG.Security.Factories;
using mRemoteNG.Tree;
using mRemoteNG.Tree.Root;
using mRemoteNG.UI.Forms;


namespace mRemoteNG.App
{
    [SupportedOSPlatform("windows")]
    public static class Export
    {
        public static void ExportToFile(ConnectionInfo selectedNode, ConnectionTreeModel connectionTreeModel)
        {
            try
            {
                SaveFilter saveFilter = new();

                using (FrmExport exportForm = new())
                {
                    // The dialog no longer asks what to export. It used to offer everything, the
                    // selected folder or the selected connection, and open on "everything"
                    // whatever had been right-clicked - so exporting one folder meant noticing a
                    // radio button first, and missing it wrote the entire tree, passwords and all,
                    // into the file. It says what it is about to do instead.
                    exportForm.ShowExportTarget(DescribeTarget(selectedNode));

                    if (exportForm.ShowDialog(FrmMain.Default) != DialogResult.OK)
                        return;

                    // Whatever was right-clicked, and nothing else. Selecting the root is how
                    // the whole tree is exported, which is the same answer by a clearer route.
                    ConnectionInfo? exportTarget =
                        selectedNode ?? connectionTreeModel.RootNodes.First(node => node is RootNodeInfo);

                    if (exportTarget == null)
                        return;

                    saveFilter.SaveUsername = exportForm.IncludeUsername;
                    saveFilter.SavePassword = exportForm.IncludePassword;
                    saveFilter.SaveDomain = exportForm.IncludeDomain;
                    saveFilter.SaveInheritance = exportForm.IncludeInheritance;
                    saveFilter.SaveCredentialId = exportForm.IncludeAssignedCredential;

                    SaveExportFile(exportForm.FileName, exportForm.SaveFormat, saveFilter, exportTarget);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("App.Export.ExportToFile() failed.", ex);
            }
        }


        /// <summary>
        /// Says in one line what is about to be written, so the dialog states its scope rather
        /// than asking for it.
        /// </summary>
        /// <remarks>
        /// A folder is given with its count. The name of a folder does not say how much is inside
        /// it, and this file may be about to carry every password in there.
        /// </remarks>
        private static string DescribeTarget(ConnectionInfo selectedNode)
        {
            if (selectedNode is RootNodeInfo || selectedNode == null)
                return Language.ExportTargetEverything;

            if (selectedNode is ContainerInfo folder)
                return string.Format(Language.ExportTargetFolder, folder.Name, CountConnections(folder));

            return string.Format(Language.ExportTargetConnection, selectedNode.Name);
        }

        private static int CountConnections(ContainerInfo container) =>
            container.Children.Sum(child => child is ContainerInfo sub ? CountConnections(sub) : 1);

        private static void SaveExportFile(string fileName,
                                           SaveFormat saveFormat,
                                           SaveFilter saveFilter,
                                           ConnectionInfo exportTarget)
        {
            try
            {
                ISerializer<ConnectionInfo, string> serializer;
                switch (saveFormat)
                {
                    case SaveFormat.mRXML:
                        ICryptographyProvider cryptographyProvider = new CryptoProviderFactoryFromSettings().Build();
                        RootNodeInfo? rootNode = exportTarget.GetRootParent() as RootNodeInfo;
                        XmlConnectionNodeSerializer28 connectionNodeSerializer = new(
                                                                                         cryptographyProvider,
                                                                                         rootNode?.PasswordString
                                                                                                 .ConvertToSecureString() ??
                                                                                         new RootNodeInfo(RootNodeType
                                                                                                              .Connection)
                                                                                             .PasswordString
                                                                                             .ConvertToSecureString(),
                                                                                         saveFilter);
                        serializer = new XmlConnectionsSerializer(cryptographyProvider, connectionNodeSerializer);
                        break;
                    case SaveFormat.mRCSV:
                        serializer =
                            new CsvConnectionsSerializerMremotengFormat(saveFilter, Runtime.CredentialProviderCatalog);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(saveFormat), saveFormat, null);
                }

                string serializedData = serializer.Serialize(exportTarget);
                FileDataProvider fileDataProvider = new(fileName);
                fileDataProvider.Save(serializedData);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace($"Export.SaveExportFile(\"{fileName}\") failed.", ex);
            }
            finally
            {
                Runtime.ConnectionsService.RemoteConnectionsSyncronizer?.Enable();
            }
        }
    }
}