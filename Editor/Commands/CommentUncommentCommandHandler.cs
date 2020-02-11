// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.Xml.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[Name (Name)]
	[ContentType (XmlContentTypeNames.XmlCore)]
	[TextViewRole (PredefinedTextViewRoles.Interactive)]
	class CommentUncommentCommandHandler :
		ICommandHandler<CommentSelectionCommandArgs>,
		ICommandHandler<UncommentSelectionCommandArgs>,
		ICommandHandler<ToggleBlockCommentCommandArgs>,
		ICommandHandler<ToggleLineCommentCommandArgs>
	{
		const string Name = nameof (CommentUncommentCommandHandler);
		const string OpenComment = "<!--";
		const string CloseComment = "-->";

		[Import]
		internal ITextUndoHistoryRegistry undoHistoryRegistry { get; set; }

		public string DisplayName => Name;

		public CommandState GetCommandState (CommentSelectionCommandArgs args) => CommandState.Available;

		public CommandState GetCommandState (UncommentSelectionCommandArgs args) => CommandState.Available;

		public CommandState GetCommandState (ToggleBlockCommentCommandArgs args) => CommandState.Available;

		public CommandState GetCommandState (ToggleLineCommentCommandArgs args) => CommandState.Available;

		enum Operation
		{
			Comment,
			Uncomment,
			Toggle
		}

		public bool ExecuteCommand (CommentSelectionCommandArgs args, CommandExecutionContext executionContext)
			=> ExecuteCommandCore (args, executionContext, Operation.Comment);

		public bool ExecuteCommand (UncommentSelectionCommandArgs args, CommandExecutionContext executionContext)
			=> ExecuteCommandCore (args, executionContext, Operation.Uncomment);

		public bool ExecuteCommand (ToggleBlockCommentCommandArgs args, CommandExecutionContext executionContext)
			=> ExecuteCommandCore (args, executionContext, Operation.Toggle);

		public bool ExecuteCommand (ToggleLineCommentCommandArgs args, CommandExecutionContext executionContext)
			=> ExecuteCommandCore (args, executionContext, Operation.Toggle);

		bool ExecuteCommandCore (EditorCommandArgs args, CommandExecutionContext context, Operation operation)
		{
			ITextView textView = args.TextView;
			ITextBuffer textBuffer = args.SubjectBuffer;

			if (!XmlBackgroundParser.TryGetParser (textBuffer, out var parser)) {
				return false;
			}

			var xmlParseResult = parser.GetOrProcessAsync(textBuffer.CurrentSnapshot, default).Result;
			var xmlDocumentSyntax = xmlParseResult.XDocument;
			if (xmlDocumentSyntax == null) {
				return false;
			}

			string description = operation.ToString();
			var selectedSpans = textView.Selection.SelectedSpans;

			using (context.OperationContext.AddScope (allowCancellation: false, description: description)) {
				ITextUndoHistory undoHistory = undoHistoryRegistry.RegisterHistory (textBuffer);

				using (ITextUndoTransaction undoTransaction = undoHistory.CreateTransaction (description)) {
					switch (operation) {
					case Operation.Comment:
						CommentSelection (textBuffer, selectedSpans, xmlDocumentSyntax);
						break;
					case Operation.Uncomment:
						UncommentSelection (textBuffer, selectedSpans, xmlDocumentSyntax);
						break;
					case Operation.Toggle:
						ToggleCommentSelection (textBuffer, selectedSpans, xmlDocumentSyntax);
						break;
					}

					undoTransaction.Complete ();
				}
			}

			return true;
		}

		public static void CommentSelection (ITextBuffer textBuffer, NormalizedSnapshotSpanCollection selectedSpans, XDocument xmlDocumentSyntax)
		{
			var commentSpans = GetCommentableSpansInSelection (xmlDocumentSyntax, selectedSpans);
			if (commentSpans == null || commentSpans.Count == 0) {
				return;
			}

			using (var edit = textBuffer.CreateEdit ()) {
				CommentSpans (edit, commentSpans);
				edit.Apply ();
			}
		}

		public static void CommentSpans (ITextEdit edit, IList<TextSpan> commentSpans)
		{
			foreach (var commentSpan in commentSpans) {
				edit.Insert (commentSpan.Start, OpenComment);
				edit.Insert (commentSpan.End, CloseComment);
			}
		}

		public static void UncommentSelection (ITextBuffer textBuffer, NormalizedSnapshotSpanCollection selectedSpans, XDocument xmlDocumentSyntax)
		{
			var commentedSpans = GetCommentedSpansInSelection (xmlDocumentSyntax, selectedSpans);
			if (commentedSpans == null || commentedSpans.Count == 0) {
				return;
			}

			using (var edit = textBuffer.CreateEdit ()) {
				UncommentSpans (edit, commentedSpans);
				edit.Apply ();
			}
		}

		public static void UncommentSpans (ITextEdit edit, IList<TextSpan> commentedSpans)
		{
			int beginCommentLength = OpenComment.Length;
			int endCommentLength = CloseComment.Length;

			foreach (var commentSpan in commentedSpans) {
				edit.Delete (commentSpan.Start, beginCommentLength);
				edit.Delete (commentSpan.End - endCommentLength, endCommentLength);
			}
		}

		public static void ToggleCommentSelection (ITextBuffer textBuffer, NormalizedSnapshotSpanCollection selectedSpans, XDocument xmlDocumentSyntax)
		{
			var commentedSpans = GetCommentedSpansInSelection (xmlDocumentSyntax, selectedSpans);
			var commentableSpans = GetCommentableSpansInSelection (xmlDocumentSyntax, selectedSpans);

			// Remove already commented blocks from the commentable spans
			var normalizedCommentedSpans = new NormalizedSpanCollection (commentedSpans.Select (ts => new Span (ts.Start, ts.Length)));
			commentableSpans = commentableSpans
				.Where (cs => !normalizedCommentedSpans.OverlapsWith (new Span (cs.Start, cs.Length)))
				.ToList ();
			if (commentedSpans.Count == 0 && commentableSpans.Count == 0) {
				return;
			}

			using (var edit = textBuffer.CreateEdit ()) {
				UncommentSpans (edit, commentedSpans);
				CommentSpans (edit, commentableSpans);
				edit.Apply ();
			}
		}

		static IList<TextSpan> GetCommentableSpansInSelection (XDocument xmlDocumentSyntax, NormalizedSnapshotSpanCollection selectedSpans)
		{
			var commentSpans = new List<TextSpan> ();

			foreach (var selectedSpan in selectedSpans) {
				var desiredCommentSpan = GetDesiredCommentSpan (selectedSpan);
				commentSpans.AddRange (xmlDocumentSyntax.GetValidCommentSpans (desiredCommentSpan));
			}

			return commentSpans;
		}

		static IList<TextSpan> GetCommentedSpansInSelection (XDocument xmlDocumentSyntax, NormalizedSnapshotSpanCollection selectedSpans)
		{
			var commentedSpans = new List<TextSpan> ();

			foreach (var selectedSpan in selectedSpans) {
				bool allowLineUncomment = true;
				if (selectedSpan.IsEmpty) {
					// For point selection, first see which comments are returned for the point span
					// If the strictly inside a commented node, just uncommented that node
					// otherwise, allow line uncomment
					var selectionCommentedSpans =
						xmlDocumentSyntax.GetCommentedSpans (new TextSpan (selectedSpan.Start, 0)).ToList ();
					foreach (var selectionCommentedSpan in selectionCommentedSpans) {
						if (selectionCommentedSpan.Contains (selectedSpan.Start) &&
							selectionCommentedSpan.Start != selectedSpan.Start &&
							selectionCommentedSpan.End != selectedSpan.Start) {
							commentedSpans.Add (selectionCommentedSpan);
							allowLineUncomment = false;
							break;
						}
					}
				}

				if (allowLineUncomment) {
					var desiredCommentSpan = GetDesiredCommentSpan (selectedSpan);
					commentedSpans.AddRange (xmlDocumentSyntax.GetCommentedSpans (desiredCommentSpan));
				}
			}

			return commentedSpans;
		}

		static TextSpan GetDesiredCommentSpan (SnapshotSpan selectedSpan)
		{
			ITextSnapshot snapshot = selectedSpan.Snapshot;
			if (!selectedSpan.IsEmpty) {
				return new TextSpan (selectedSpan.Start, selectedSpan.Length);
			}

			// Comment line for empty selections (first to last non-whitespace character)
			var line = snapshot.GetLineFromPosition (selectedSpan.Start);

			int? start = null;
			for (int i = line.Start; i < line.End.Position; i++) {
				if (!IsWhiteSpace (snapshot[i])) {
					start = i;
					break;
				}
			}

			if (start == null) {
				return new TextSpan (selectedSpan.Start, 0);
			}

			int end = start.Value;
			for (int i = line.End.Position - 1; i >= end; i--) {
				if (!IsWhiteSpace (snapshot[i])) {
					// need to add 1 since end is exclusive
					end = i + 1;
					break;
				}
			}

			return TextSpan.FromBounds (start.Value, end);
		}

		static bool IsWhiteSpace (char c)
		{
			return c == ' ' || c == '\t' || (c > (char)128 && char.IsWhiteSpace (c));
		}
	}

	public static class CommentUtilities
	{
		public static IEnumerable<TextSpan> GetValidCommentSpans (this XContainer node, TextSpan commentSpan)
		{
			return GetCommentSpans (node, commentSpan, returnComments: false);
		}

		public static IEnumerable<TextSpan> GetCommentedSpans (this XContainer node, TextSpan commentSpan)
		{
			return GetCommentSpans (node, commentSpan, returnComments: true);
		}

		static void VisitChildren (XNode node, Action<XNode> action)
		{
			action (node);
			if (node is XContainer container) {
				foreach (var child in container.Nodes) {
					VisitChildren (child, action);
				}
			}
		}

		static IEnumerable<TextSpan> GetCommentSpans (this XContainer node, TextSpan commentSpan, bool returnComments)
		{
			var commentSpans = new List<TextSpan> ();

			TextSpan validCommentRegion = node.GetValidCommentRegion (commentSpan);
			int currentStart = validCommentRegion.Start;

			// Creates comments such that current comments are excluded
			VisitChildren (node, n => {
				if (n is XComment comment) {
					var commentNodeSpan = n.Span;
					if (returnComments)
						commentSpans.Add (commentNodeSpan);
					else {
						var validCommentSpan = TextSpan.FromBounds (currentStart, commentNodeSpan.Start);
						if (validCommentSpan.Length != 0) {
							commentSpans.Add (validCommentSpan);
						}

						currentStart = commentNodeSpan.End;
					}
				}
			});

			if (!returnComments) {
				if (currentStart <= validCommentRegion.End) {
					var remainingCommentSpan = TextSpan.FromBounds (currentStart, validCommentRegion.End);
					if (remainingCommentSpan.Equals(validCommentRegion) || remainingCommentSpan.Length != 0) {
						// Comment any remaining uncommented area
						commentSpans.Add (remainingCommentSpan);
					}
				}
			}

			return commentSpans;
		}

		public static TextSpan GetValidCommentRegion (this XContainer node, TextSpan commentSpan)
		{
			var commentSpanStart = GetCommentRegion (node, commentSpan.Start, commentSpan);

			if (commentSpan.Length == 0)
				return commentSpanStart;

			var commentSpanEnd = GetCommentRegion (node, commentSpan.End - 1, commentSpan);

			return TextSpan.FromBounds (
				start: Math.Min (commentSpanStart.Start, commentSpanEnd.Start),
				end: Math.Max (Math.Max (commentSpanStart.End, commentSpanEnd.End), commentSpan.End));
		}

		static TextSpan GetCommentRegion (this XContainer node, int position, TextSpan span)
		{
			var nodeAtPosition = node.FindAtOffset (position);

			// if the selection starts or ends in text, we want to preserve the 
			// exact span the user has selected and split the text at that boundary
			if (nodeAtPosition is XText) {
				return span;
			}

			if (nodeAtPosition is XComment ||
				nodeAtPosition is XProcessingInstruction) {
				return nodeAtPosition.Span;
			}

			var nearestParentElement = nodeAtPosition.SelfAndParentsOfType<XElement> ().FirstOrDefault ();
			if (nearestParentElement == null) {
				return new TextSpan (position, 0);
			}

			var endSpan = nearestParentElement.ClosingTag;
			if (endSpan == null) {
				return nodeAtPosition.Span;
			}

			int start = nearestParentElement.Span.Start;
			return new TextSpan (start, endSpan.Span.End - start);
		}
	}
}