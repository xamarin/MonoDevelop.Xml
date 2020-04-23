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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Platform;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Tasks;
using MonoDevelop.TextEditor;
using MonoDevelop.Xml.Editor;

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
	public sealed class CreateSchemaCommandArgs : EditorCommandArgs
	{
		public CreateSchemaCommandArgs (ITextView textView, ITextBuffer subjectBuffer) : base (textView, subjectBuffer)
		{
		}
	}

	[Name ("XmlCommandHandler")]
	[ContentType ("xml")]
	[TextViewRole (PredefinedTextViewRoles.Interactive)]
	[Export]
	[Export (typeof (ICommandHandler))]
	public class XmlCommandHandler : ICommandHandler<CreateSchemaCommandArgs>
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
						LoggingService.LogError (msg, ex);
						monitor.ReportError (msg, ex);
					}
				}
			} catch (Exception ex) {

				MessageService.ShowError (ex.Message);
			}

			return false;
		}

		CommandState ICommandHandler<CreateSchemaCommandArgs>.GetCommandState (CreateSchemaCommandArgs args)
		{
			return CommandState.Available;
		}
	}
}
