using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin (
	"Xml",
	Namespace = "MonoDevelop",
	Version = MonoDevelop.BuildInfo.Version,
	Category = "IDE extensions"
)]

[assembly: AddinName ("Xml Editor")]
[assembly: AddinCategory ("IDE extensions")]
[assembly: AddinDescription ("Editing support for XML files (new editor)")]
[assembly: AddinAuthor ("Mikayla Hutchinson")]

[assembly: AddinDependency ("Core", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency ("Ide", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency ("TextEditor", MonoDevelop.BuildInfo.Version)]
