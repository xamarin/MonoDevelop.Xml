// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.Xml.Editor.Tagging
{
	class StructureTagger : ITagger<IStructureTag>
	{
		private ITextBuffer buffer;
		private StructureTaggerProvider provider;
		private readonly XmlBackgroundParser parser;
		private static readonly IEnumerable<ITagSpan<IStructureTag>> emptyTagList = Array.Empty<ITagSpan<IStructureTag>> ();

		public StructureTagger (ITextBuffer buffer, StructureTaggerProvider provider)
		{
			this.buffer = buffer;
			this.provider = provider;
			parser = XmlBackgroundParser.GetParser (buffer);
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<IStructureTag>> GetTags (NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0) {
				return emptyTagList;
			}

			var snapshot = spans[0].Snapshot;

			var parseTask = parser.GetOrProcessAsync (snapshot, default);

			// for most files we get the results very quickly so try to do it inline
			parseTask.Wait (TimeSpan.FromMilliseconds (50));

			if (parseTask.IsCompleted) {
				return GetTags(parseTask.Result, spans, snapshot);
			} else {
				parseTask.ContinueWith(t =>
				{
					RaiseTagsChanged();
				});
			}

			return emptyTagList;
		}

		private void RaiseTagsChanged()
		{
			var snapshot = buffer.CurrentSnapshot;
			var args = new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length));
			TagsChanged?.Invoke(this, args);
		}

		private IEnumerable<ITagSpan<IStructureTag>> GetTags(XmlParseResult xmlParseResult, NormalizedSnapshotSpanCollection spans, ITextSnapshot snapshot)
		{
			var root = xmlParseResult.XDocument;

			List<ITagSpan<IStructureTag>> resultList = new List<ITagSpan<IStructureTag>>();

			foreach (var snapshotSpan in spans)
			{
				var nodes = root.GetNodesIntersectingRange(new TextSpan(snapshotSpan.Span.Start, snapshotSpan.Span.Length));
				foreach (var node in nodes)
				{
					var nodeSpan = node.OuterSpan;
					if (nodeSpan.Start < 0 || nodeSpan.End > snapshot.Length)
					{
						continue;
					}

					var outliningSpan = new Span(nodeSpan.Start, nodeSpan.Length);
					var startLine = snapshot.GetLineNumberFromPosition(outliningSpan.Start);
					var endLine = snapshot.GetLineNumberFromPosition(outliningSpan.End);
					if (startLine == endLine)
					{
						// ignore single-line nodes 
						continue;
					}

					var tagSnapshotSpan = new SnapshotSpan(snapshot, outliningSpan);
					var structureTag = new StructureTag(snapshot, outliningSpan: outliningSpan, isCollapsible: true, collapsedForm: "...");
					var tagSpan = new TagSpan<IStructureTag>(tagSnapshotSpan, structureTag);
					resultList.Add(tagSpan);
				}
			}

			return resultList;
		}
	}
}