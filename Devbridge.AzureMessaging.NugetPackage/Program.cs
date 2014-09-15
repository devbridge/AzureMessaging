using System;
using System.IO;
using System.Reflection;
using Devbridge.AzureMessaging.Interfaces;

namespace Devbridge.AzureMessaging.NugetPackage
{
    /// <summary>
    /// Fake program to force solution to build NuGet package.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Mains the specified args.
        /// </summary>
        /// <param name="args">The args.</param>
        public static void Main(string[] args)
        {
            var nugetTemplateFile = args[0];    //DevBridge.AzureMessaging.nuspec.template;
            var nugetFile = args[1];            //DevBridge.AzureMessaging.nuspec;
            var version = GetVersion();

            using (var sr = new StreamReader(nugetTemplateFile))
            {
                try
                {
                    var templateFile = sr.ReadToEnd();
                    templateFile = templateFile.Replace("@PackageVersion@", version);

                    using (var sw = new StreamWriter(nugetFile))
                    {
                        try
                        {
                            sw.Write(templateFile);
                        }
                        finally
                        {
                            sw.Close();
                        }
                    }
                }
                finally
                {
                    sr.Close();
                }
            }
        }

        private static string GetVersion()
        {
            var assembly = typeof(IMessageService).Assembly;
            var assemblyInformationVersion = Attribute.GetCustomAttributes(assembly, typeof(AssemblyInformationalVersionAttribute));

            if (assemblyInformationVersion.Length > 0)
            {
                var informationVersion = ((AssemblyInformationalVersionAttribute)assemblyInformationVersion[0]);

                return informationVersion.InformationalVersion;
            }

            return assembly.GetName().Version.ToString(3);
        }
    }
}
