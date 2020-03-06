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

		[Import]
		internal IEditorOperationsFactoryService editorOperationsFactoryService { get; set; }

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

			var xmlParseResult = parser.GetOrProcessAsync (textBuffer.CurrentSnapshot, default).Result;
			var xmlDocumentSyntax = xmlParseResult.XDocument;
			if (xmlDocumentSyntax == null) {
				return false;
			}

			string description = operation.ToString ();

			var editorOperations = editorOperationsFactoryService.GetEditorOperations (textView);

			var multiSelectionBroker = textView.GetMultiSelectionBroker ();
			var selectedSpans = multiSelectionBroker.AllSelections.Select (selection => selection.Extent);

			using (context.OperationContext.AddScope (allowCancellation: false, description: description)) {
				ITextUndoHistory undoHistory = undoHistoryRegistry.RegisterHistory (textBuffer);

				using (ITextUndoTransaction undoTransaction = undoHistory.CreateTransaction (description)) {
					switch (operation) {
					case Operation.Comment:
						CommentSelection (textBuffer, selectedSpans, xmlDocumentSyntax, editorOperations, multiSelectionBroker);
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

		public static void CommentSelection (
			ITextBuffer textBuffer,
			IEnumerable<VirtualSnapshotSpan> selectedSpans,
			XDocument xmlDocumentSyntax,
			IEditorOperations editorOperations = null,
			IMultiSelectionBroker multiSelectionBroker = null)
		{
			var snapshot = textBuffer.CurrentSnapshot;

			var spansToExpandIntoComments = new List<SnapshotSpan> ();
			var newCommentInsertionPoints = new List<VirtualSnapshotPoint> ();
			foreach (var selectedSpan in selectedSpans) {
				if (selectedSpan.IsEmpty && string.IsNullOrWhiteSpace (snapshot.GetLineFromPosition (selectedSpan.Start.Position).GetText ())) {
					newCommentInsertionPoints.Add (selectedSpan.Start);
				} else {
					spansToExpandIntoComments.Add (selectedSpan.SnapshotSpan);
				}
			}

			NormalizedSnapshotSpanCollection commentSpans = null;
			if (spansToExpandIntoComments.Any ()) {
				commentSpans = GetCommentableSpansInSelection (xmlDocumentSyntax, spansToExpandIntoComments);
			}

			using (var edit = textBuffer.CreateEdit ()) {
				if (commentSpans?.Any () == true) {
					CommentSpans (edit, commentSpans);
				}

				if (newCommentInsertionPoints?.Any () == true) {
					CommentEmptySpans (edit, newCommentInsertionPoints, editorOperations);
				}

				edit.Apply ();
			}

			var newSnapshot = textBuffer.CurrentSnapshot;

			var translatedInsertionPoints = newCommentInsertionPoints.Select (p => p.TranslateTo (newSnapshot)).ToHashSet ();

			if (multiSelectionBroker != null) {
				multiSelectionBroker.PerformActionOnAllSelections (transformer => {
					if (translatedInsertionPoints.Contains (transformer.Selection.ActivePoint)) {
						transformer.MoveTo (
							new VirtualSnapshotPoint (transformer.Selection.End.Position - CloseComment.Length - 1),
							select: false,
							insertionPointAffinity: PositionAffinity.Successor);
					}
				});
			}
		}

		private static void CommentEmptySpans (ITextEdit edit, IEnumerable<VirtualSnapshotPoint> virtualPoints, IEditorOperations editorOperations)
		{
			foreach (var virtualPoint in virtualPoints) {
				if (virtualPoint.IsInVirtualSpace) {
					string leadingWhitespace;
					if (editorOperations != null) {
						leadingWhitespace = editorOperations.GetWhitespaceForVirtualSpace (virtualPoint);
					} else {
						leadingWhitespace = new string (' ', virtualPoint.VirtualSpaces);
					}

					edit.Insert (virtualPoint.Position, leadingWhitespace);
					edit.Insert (virtualPoint.Position, OpenComment);
					edit.Insert (virtualPoint.Position, "  ");
					edit.Insert (virtualPoint.Position, CloseComment);
				}
			}
		}

		public static void CommentSpans (ITextEdit edit, NormalizedSnapshotSpanCollection commentSpans)
		{
			foreach (var commentSpan in commentSpans) {
				edit.Insert (commentSpan.Start, OpenComment);
				edit.Insert (commentSpan.End, CloseComment);
			}
		}

		public static void UncommentSelection (ITextBuffer textBuffer, IEnumerable<VirtualSnapshotSpan> selectedSpans, XDocument xmlDocumentSyntax)
		{
			var commentedSpans = GetCommentedSpansInSelection (xmlDocumentSyntax, selectedSpans);
			if (commentedSpans == null || !commentedSpans.Any ()) {
				return;
			}

			using (var edit = textBuffer.CreateEdit ()) {
				UncommentSpans (edit, commentedSpans);
				edit.Apply ();
			}
		}

		public static void UncommentSpans (ITextEdit edit, IEnumerable<SnapshotSpan> commentedSpans)
		{
			int beginCommentLength = OpenComment.Length;
			int endCommentLength = CloseComment.Length;

			foreach (var commentSpan in commentedSpans) {
				edit.Delete (commentSpan.Start, beginCommentLength);
				edit.Delete (commentSpan.End - endCommentLength, endCommentLength);
			}
		}

		public static void ToggleCommentSelection (ITextBuffer textBuffer, IEnumerable<VirtualSnapshotSpan> selectedSpans, XDocument xmlDocumentSyntax)
		{
			var commentedSpans = GetCommentedSpansInSelection (xmlDocumentSyntax, selectedSpans);
			var commentableSpans = GetCommentableSpansInSelection (xmlDocumentSyntax, selectedSpans.Select (s => s.SnapshotSpan));

			// Remove already commented blocks from the commentable spans
			var normalizedCommentedSpans = new NormalizedSnapshotSpanCollection (commentedSpans);
			commentableSpans = new NormalizedSnapshotSpanCollection (commentableSpans
				.Where (cs => !normalizedCommentedSpans.OverlapsWith (cs))
				.ToList ());
			if (!commentedSpans.Any () && !commentableSpans.Any ()) {
				return;
			}

			using (var edit = textBuffer.CreateEdit ()) {
				UncommentSpans (edit, commentedSpans);
				CommentSpans (edit, commentableSpans);
				edit.Apply ();
			}
		}

		static NormalizedSnapshotSpanCollection GetCommentableSpansInSelection (XDocument xmlDocumentSyntax, IEnumerable<SnapshotSpan> selectedSpans)
		{
			var commentSpans = new List<SnapshotSpan> ();
			var snapshot = selectedSpans.First ().Snapshot;

			var validSpans = xmlDocumentSyntax.GetValidCommentSpans (selectedSpans.Select (s => GetDesiredCommentSpan (s).ToSpan ()));

			foreach (var singleValidSpan in validSpans) {
				var snapshotSpan = new SnapshotSpan (snapshot, new Span (singleValidSpan.Start, singleValidSpan.Length));
				commentSpans.Add (snapshotSpan);
			}

			return new NormalizedSnapshotSpanCollection (commentSpans);
		}

		static IEnumerable<SnapshotSpan> GetCommentedSpansInSelection (XDocument xmlDocumentSyntax, IEnumerable<VirtualSnapshotSpan> selectedSpans)
		{
			var commentedSpans = new List<SnapshotSpan> ();
			var snapshot = selectedSpans.First ().Snapshot;

			foreach (var selectedSpan in selectedSpans) {
				bool allowLineUncomment = true;
				if (selectedSpan.IsEmpty) {
					// For point selection, first see which comments are returned for the point span
					// If the strictly inside a commented node, just uncommented that node
					// otherwise, allow line uncomment
					var start = selectedSpan.Start.Position.Position;
					var selectionCommentedSpans =
						xmlDocumentSyntax.GetCommentedSpans (new[] { new Span (start, 0) }).ToList ();
					foreach (var selectionCommentedSpan in selectionCommentedSpans) {
						if (selectionCommentedSpan.Contains (start) &&
							selectionCommentedSpan.Start != start &&
							selectionCommentedSpan.End != start) {
							var snapshotSpan = new SnapshotSpan (snapshot, selectionCommentedSpan.Start, selectionCommentedSpan.Length);
							commentedSpans.Add (snapshotSpan);
							allowLineUncomment = false;
							break;
						}
					}
				}

				if (allowLineUncomment) {
					var desiredCommentSpan = GetDesiredCommentSpan (selectedSpan.SnapshotSpan);
					var commentedSpans2 = xmlDocumentSyntax.GetCommentedSpans (new[] { desiredCommentSpan.ToSpan () });
					foreach (var commentedSpan2 in commentedSpans2) {
						var snapshotSpan = new SnapshotSpan (snapshot, commentedSpan2.Start, commentedSpan2.Length);
						commentedSpans.Add (snapshotSpan);
					}
				}
			}

			return commentedSpans;
		}

		static TextSpan GetDesiredCommentSpan (SnapshotSpan selectedSpan)
		{
			ITextSnapshot snapshot = selectedSpan.Snapshot;
			if (!selectedSpan.IsEmpty) {
				int selectionLength = selectedSpan.Length;

				// tweak the selection end to not include the last line break
				while (selectionLength > 0 && IsLineBreak (snapshot[selectedSpan.Start + selectionLength - 1])) {
					selectionLength--;
				}

				return new TextSpan (selectedSpan.Start, selectionLength);
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

		static bool IsLineBreak (char c)
		{
			return c == '\n' || c == '\r';
		}
	}

	public static class CommentUtilities
	{
		public static IEnumerable<TextSpan> GetValidCommentSpans (this XContainer node, IEnumerable<Span> commentSpans)
		{
			return GetCommentSpans (node, commentSpans, returnComments: false);
		}

		public static IEnumerable<TextSpan> GetCommentedSpans (this XContainer node, IEnumerable<Span> commentSpans)
		{
			return GetCommentSpans (node, commentSpans, returnComments: true);
		}

		static IEnumerable<TextSpan> GetCommentSpans (this XContainer node, IEnumerable<Span> selectedSpans, bool returnComments)
		{
			var commentSpans = new List<TextSpan> ();

			var regions = new List<Span> ();

			foreach (var selectedSpan in selectedSpans) {
				var region = node.GetValidCommentRegion (selectedSpan.ToTextSpan ());
				regions.Add (region.ToSpan ());
			}

			var allRegions = new NormalizedSpanCollection (regions);
			var allRegionsSpan = allRegions.Envelope ();

			int currentStart = allRegionsSpan.Start;

			// Creates comments such that current comments are excluded
			var parentNode = node.GetNodeContainingRange (allRegionsSpan);

			parentNode.VisitSelfAndChildren (child => {
				if (child is XComment comment) {
					// ignore comments outside our range
					if (!comment.Span.Intersects (allRegionsSpan)) {
						return;
					}

					var commentNodeSpan = comment.Span;
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
				if (currentStart <= allRegionsSpan.End) {
					var remainingCommentSpan = TextSpan.FromBounds (currentStart, allRegionsSpan.End);
					if (remainingCommentSpan.Equals (allRegionsSpan) || remainingCommentSpan.Length != 0) {
						// Comment any remaining uncommented area
						commentSpans.Add (remainingCommentSpan);
					}
				}
			}

			return commentSpans;
		}

		public static TextSpan ToTextSpan (this Span span)
		{
			return new TextSpan (span.Start, span.Length);
		}

		public static Span ToSpan (this TextSpan textSpan)
		{
			return new Span (textSpan.Start, textSpan.Length);
		}

		public static TextSpan Envelope (this NormalizedSpanCollection spans)
		{
			if (spans == null || spans.Count == 0) {
				return default;
			}

			var first = spans[0];
			var last = spans[spans.Count - 1];
			return new TextSpan (first.Start, last.End - first.Start);
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