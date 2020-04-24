//
// MonoDevelop XML Editor
//
// Copyright (C) 2006 Matthew Ward
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Xml.Schema;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Platform;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Tasks;
using MonoDevelop.Projects;
using MonoDevelop.TextEditor;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.Xml.Editor
{
    public enum XmlCommands
    {
        CreateSchema,
        Validate,
        AssignStylesheet,
        OpenStylesheet,
        RunXslTransform,
        GoToSchemaDefinition
    }
}

namespace Microsoft.VisualStudio.Text.Editor.Commanding.Commands.Xml
{
	public sealed class ValidateCommandArgs : EditorCommandArgs
	{
		public ValidateCommandArgs (ITextView textView, ITextBuffer subjectBuffer) : base (textView, subjectBuffer)
		{
		}
	}

	public sealed class CreateSchemaCommandArgs : EditorCommandArgs
	{
		public CreateSchemaCommandArgs (ITextView textView, ITextBuffer subjectBuffer) : base (textView, subjectBuffer)
		{
		}
	}

	public sealed class GoToSchemaDefinitionCommandArgs : EditorCommandArgs
	{
		public GoToSchemaDefinitionCommandArgs (ITextView textView, ITextBuffer subjectBuffer) : base (textView, subjectBuffer)
		{
		}
	}

	public sealed class AssignStylesheetCommandArgs : EditorCommandArgs
	{
		public AssignStylesheetCommandArgs (ITextView textView, ITextBuffer subjectBuffer) : base (textView, subjectBuffer)
		{
		}
	}

	public sealed class OpenStylesheetCommandArgs : EditorCommandArgs
	{
		public OpenStylesheetCommandArgs (ITextView textView, ITextBuffer subjectBuffer) : base (textView, subjectBuffer)
		{
		}
	}

	public sealed class RunXslTransformCommandArgs : EditorCommandArgs
	{
		public RunXslTransformCommandArgs (ITextView textView, ITextBuffer subjectBuffer) : base (textView, subjectBuffer)
		{
		}
	}

	[Name ("XmlCommandHandler")]
	[ContentType ("xml")]
	[TextViewRole (PredefinedTextViewRoles.Interactive)]
	[Export]
	[Export (typeof (ICommandHandler))]
	public class XmlCommandHandler :
		ICommandHandler<ValidateCommandArgs>,
		ICommandHandler<CreateSchemaCommandArgs>,
		ICommandHandler<GoToSchemaDefinitionCommandArgs>,
		ICommandHandler<AssignStylesheetCommandArgs>,
		ICommandHandler<OpenStylesheetCommandArgs>,
		ICommandHandler<RunXslTransformCommandArgs>
	{
		public string DisplayName => "Xml Command Handler";

		bool ICommandHandler<CreateSchemaCommandArgs>.ExecuteCommand (CreateSchemaCommandArgs args, CommandExecutionContext executionContext)
		{
			try {
				IdeServices.TaskService.Errors.Clear ();

				var buffer = args.SubjectBuffer;
				string xml = buffer.CurrentSnapshot.GetText();
				using (ProgressMonitor monitor = XmlEditorService.GetMonitor ()) {
					monitor.BeginTask (GettextCatalog.GetString ("Creating schema..."), 0);
					try {
						string schema = XmlEditorService.CreateSchema (xml, useTabs: false, tabSize: 4);

						string filePath = buffer.GetFilePathOrNull () ?? "schema.xml";
						filePath = XmlEditorService.GenerateFileName (filePath, "{0}.xsd");
						IdeApp.Workbench.NewDocument (filePath, "application/xml", schema);
						monitor.ReportSuccess (GettextCatalog.GetString ("Schema created."));
					} catch (Exception ex) {
						string msg = GettextCatalog.GetString ("Error creating XML schema.");
						MonoDevelop.Core.LoggingService.LogError (msg, ex);
						monitor.ReportError (msg, ex);
					}
				}
			} catch (Exception ex) {

				MessageService.ShowError (ex.Message);
			}

			return false;
		}

		bool ICommandHandler<ValidateCommandArgs>.ExecuteCommand (ValidateCommandArgs args, CommandExecutionContext executionContext)
		{
			IdeServices.TaskService.Errors.Clear ();

			var buffer = args.SubjectBuffer;
			string xml = buffer.CurrentSnapshot.GetText ();
			string filePath = buffer.GetFilePathOrNull () ?? "file.xml";

			ValidateAsync (xml, filePath).Ignore ();

			return true;
		}

		private async Task ValidateAsync(string xml, FilePath filePath)
		{
			List<BuildError> errors;

			using (ProgressMonitor monitor = XmlEditorService.GetMonitor ()) {
				monitor.BeginTask (GettextCatalog.GetString ("Validating {0}...", filePath.FileName), 0);

				if (filePath.HasExtension (".xsd"))
					errors = await XmlEditorService.ValidateSchema (xml, filePath, monitor.CancellationToken);
				else
					errors = await XmlEditorService.ValidateXml (xml, filePath, monitor.CancellationToken);

				if (errors.Count > 0) {
					foreach (var err in errors) {
						monitor.Log.WriteLine (err);
					}
					monitor.ReportError ("Validation failed");
				} else {
					monitor.ReportSuccess ("Validation succeeded");
				}

				UpdateErrors (errors);
			}
		}

		void UpdateErrors (List<BuildError> errors)
		{
			IdeServices.TaskService.Errors.ClearByOwner (this);
			if (errors.Count == 0)
				return;
			foreach (var error in errors) {
				IdeServices.TaskService.Errors.Add (new TaskListEntry (error, owner: this));
			}
			IdeServices.TaskService.ShowErrors ();
		}

		bool ICommandHandler<GoToSchemaDefinitionCommandArgs>.ExecuteCommand (GoToSchemaDefinitionCommandArgs args, CommandExecutionContext executionContext)
		{
			var buffer = args.SubjectBuffer;
			string xml = buffer.CurrentSnapshot.GetText ();
			string filePath = buffer.GetFilePathOrNull () ?? "file.xml";
			var context = XmlEditorContext.Get (args.TextView);

			try {
				XmlSchemaObject schemaObject = context.GetSchemaObjectSelected ();

				// Open schema if resolved
				if (schemaObject != null && schemaObject.SourceUri != null && schemaObject.SourceUri.Length > 0) {

					string schemaFileName = schemaObject.SourceUri.Replace ("file:/", String.Empty);
					IdeApp.Workbench.OpenDocument (
						schemaFileName,
						Math.Max (1, schemaObject.LineNumber),
						Math.Max (1, schemaObject.LinePosition));
				}
			} catch (Exception ex) {
				MonoDevelop.Core.LoggingService.LogError ("Could not open document.", ex);
				MessageService.ShowError ("Could not open document.", ex);
			}

			return true;
		}

		bool ICommandHandler<AssignStylesheetCommandArgs>.ExecuteCommand (AssignStylesheetCommandArgs args, CommandExecutionContext executionContext)
		{
			return true;
		}

		bool ICommandHandler<OpenStylesheetCommandArgs>.ExecuteCommand (OpenStylesheetCommandArgs args, CommandExecutionContext executionContext)
		{
			return true;
		}

		bool ICommandHandler<RunXslTransformCommandArgs>.ExecuteCommand (RunXslTransformCommandArgs args, CommandExecutionContext executionContext)
		{
			return true;
		}

		CommandState ICommandHandler<ValidateCommandArgs>.GetCommandState (ValidateCommandArgs args) => CommandState.Available;
		CommandState ICommandHandler<CreateSchemaCommandArgs>.GetCommandState (CreateSchemaCommandArgs args) => CommandState.Available;
		CommandState ICommandHandler<GoToSchemaDefinitionCommandArgs>.GetCommandState (GoToSchemaDefinitionCommandArgs args) => CommandState.Available;
		CommandState ICommandHandler<AssignStylesheetCommandArgs>.GetCommandState (AssignStylesheetCommandArgs args) => CommandState.Available;
		CommandState ICommandHandler<OpenStylesheetCommandArgs>.GetCommandState (OpenStylesheetCommandArgs args) => CommandState.Available;
		CommandState ICommandHandler<RunXslTransformCommandArgs>.GetCommandState (RunXslTransformCommandArgs args) => CommandState.Available;
	}
}
