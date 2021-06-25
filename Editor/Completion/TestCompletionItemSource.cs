using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;

namespace MonoDevelop.Xml.Editor.Completion
{
	public class TestCompletionItemSource : IAsyncCompletionSource
	{
		private ImmutableArray<CompletionItem> sampleItems;
		public TestCompletionItemSource ()
		{
			sampleItems = ImmutableArray.Create (
				new CompletionItem ("Hello", this),
				new CompletionItem ("World", this));
		}

		public Task<CompletionContext> GetCompletionContextAsync (IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
		{
			session.Properties["LineNumber"] = triggerLocation.GetContainingLine ().LineNumber;
			return Task.FromResult(new CompletionContext (sampleItems));
		}

		public Task<object> GetDescriptionAsync (IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
		{
			var content = new ContainerElement (
				ContainerElementStyle.Wrapped,
				new ClassifiedTextElement (
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Keyword, "Hello!"),
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Identifier, " This is a sample item")));
			var lineInfo = new ClassifiedTextElement (
					new ClassifiedTextRun (
						PredefinedClassificationTypeNames.Comment,
						"You are on line " + ((int)(session.Properties["LineNumber"]) + 1).ToString ()));
			var timeInfo = new ClassifiedTextElement (
					new ClassifiedTextRun (
						PredefinedClassificationTypeNames.Identifier,
						"and it is " + DateTime.Now.ToShortTimeString ()));

			return Task.FromResult(new ContainerElement (
				ContainerElementStyle.Stacked,
				content,
				lineInfo,
				timeInfo) as object);
		}

		public CompletionStartData InitializeCompletion (CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
		{
			// purpose?
			return CompletionStartData.ParticipatesInCompletionIfAny;
		}
	}
}
