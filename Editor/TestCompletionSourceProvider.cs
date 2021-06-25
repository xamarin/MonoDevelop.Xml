using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.Xml.Editor
{
	//[Export (typeof (IAsyncCompletionSourceProvider))]
	//[ContentType (XmlContentTypeNames.XmlCore)]
	//[Name ("Test xml completion item source")]
	public class TestCompletionSourceProvider : IAsyncCompletionSourceProvider
	{
		Lazy<TestCompletionItemSource> source = new Lazy<TestCompletionItemSource> (() => new TestCompletionItemSource ());
		public TestCompletionSourceProvider ()
		{
		}

		public IAsyncCompletionSource GetOrCreate (ITextView textView)
		{
			return source.Value;
		}
	}
}
