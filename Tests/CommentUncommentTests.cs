// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.MiniEditor;
using Microsoft.VisualStudio.Text;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Commands;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;
using NUnit.Framework;

namespace MonoDevelop.Xml.Tests
{
	[TestFixture]
	public class CommentUncommentTests : EditorTestBase
	{
		protected override string ContentTypeName => XmlContentTypeNames.XmlCore;

		protected override (EditorEnvironment, EditorCatalog) InitializeEnvironment () => XmlTestEnvironment.EnsureInitialized ();

		[Test]
		[TestCase (@"[]<x>
</x>", @"<!--<x>
</x>-->")]
		[TestCase (@"<x>[]
</x>", @"<!--<x>
</x>-->")]
		[TestCase (@"[<x>]
</x>", @"<!--<x>
</x>-->")]
		[TestCase (@"[<x>
</x>]", @"<!--<x>
</x>-->")]
		[TestCase (@"<x>
[]</x>", @"<!--<x>
</x>-->")]
		[TestCase (@"<x>
  [<a></a>]
</x>", @"<x>
  <!--<a></a>-->
</x>")]
		[TestCase (@"[]<x />", @"<!--<x />-->")]
		[TestCase (@"<x />[]", @"<!--<x />-->")]
		[TestCase (@"<x
[a]=""a""/>", @"<!--<x
a=""a""/>-->")]
		[TestCase (@"[]<?xml ?>", @"<!--<?xml ?>-->")]
		[TestCase (@"[]<!--x-->", @"<!--x-->")]
		public void TestComment (string sourceText, string expectedText)
		{
			var (text, spans) = GetTextAndSpans (sourceText);
			var buffer = CreateTextBuffer (text);
			var parser = XmlBackgroundParser.GetParser<XmlBackgroundParser> (buffer);

			var snapshotSpans = new NormalizedSnapshotSpanCollection (buffer.CurrentSnapshot, spans);

			CommentUncommentCommandHandler.CommentSelection (buffer, snapshotSpans, parser.GetOrProcessAsync (buffer.CurrentSnapshot, default).Result.XDocument);

			var actualText = buffer.CurrentSnapshot.GetText ();

			Assert.AreEqual (expectedText, actualText);
		}

		(string text, NormalizedSpanCollection spans) GetTextAndSpans(string textWithSpans)
		{
			var spans = new List<Span> ();

			var sb = new StringBuilder ();

			int index = 0;
			int start = 0;
			for (int i = 0; i < textWithSpans.Length; i++) {
				char ch = textWithSpans[i];
				if (ch == '[') {
					start = index;
				} else if (ch == ']') {
					var span = new Span (start, index - start);
					spans.Add (span);
				} else {
					sb.Append (ch);
					index++;
				}
			}

			return (sb.ToString(), new NormalizedSpanCollection(spans));
		}
	}
}