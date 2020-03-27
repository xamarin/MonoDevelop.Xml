using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin (
	"MonoDevelop.Xml",
	Namespace = "MonoDevelop.Xml",
	Version = MonoDevelop.BuildInfo.Version,
	Category = "IDE Extensions"
)]

[assembly: AddinName ("Xml Editor")]
[assembly: AddinCategory ("IDE extensions")]
[assembly: AddinDescription ("Editing support for XML files")]
[assembly: AddinAuthor ("Mikayla Hutchinson")]

[assembly: AddinDependency ("Core", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency ("Ide", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency ("TextEditor", MonoDevelop.BuildInfo.Version)]
