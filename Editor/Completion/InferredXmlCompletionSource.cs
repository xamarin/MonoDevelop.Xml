using System;
using Microsoft.VisualStudio.Text.Editor;

namespace MonoDevelop.Xml.Editor.Completion
{
	public class InferredXmlCompletionSource : XmlCompletionSource
	{
		public InferredXmlCompletionSource (ITextView textView)
			: base(textView)
		{
		}
	}
}
