using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin(Id = "VSBuildMonitor", Namespace = "com.lobger.vsbuildmonitor", Version = "1.0")]
[assembly: AddinName("VS Build Monitior Extension")]
[assembly: AddinCategory("IDE extensions")]
[assembly: AddinDescription("Monitor the build process from external devices")]
[assembly: AddinAuthor("Martin Løbger")]

[assembly: AddinDependency("::MonoDevelop.Core", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency("::MonoDevelop.Ide", MonoDevelop.BuildInfo.Version)]
