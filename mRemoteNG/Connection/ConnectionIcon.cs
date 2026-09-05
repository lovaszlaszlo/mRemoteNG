using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Versioning;
using mRemoteNG.App;
using mRemoteNG.App.Info;


namespace mRemoteNG.Connection
{
    [SupportedOSPlatform("windows")]
    public class ConnectionIcon : StringConverter
    {
        public static string[] Icons = { };

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(Icons);
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }

        public static System.Drawing.Icon? FromString(string iconName)
        {
            try
            {
                string iconFolder = $"{GeneralAppInfo.HomePath}\\Icons";
                string iconPath = $"{iconFolder}\\{iconName}.ico";

                if (System.IO.File.Exists(iconPath))
                    return new System.Drawing.Icon(iconPath);

                // The loader gathers names from subfolders too, so anyone who sorted their
                // icons into folders got the names in the list and nothing drawn beside the
                // connection - this looked only in the top folder. Searched properly now,
                // and only once the direct path has missed, so the usual case still costs
                // a single File.Exists.
                if (System.IO.Directory.Exists(iconFolder))
                {
                    string found = System.IO.Directory
                                         .EnumerateFiles(iconFolder, $"{iconName}.ico",
                                                         System.IO.SearchOption.AllDirectories)
                                         .FirstOrDefault();

                    if (found != null)
                        return new System.Drawing.Icon(found);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg, $"Couldn't get Icon from String" + Environment.NewLine + ex.Message);
            }

            return null;
        }
    }
}