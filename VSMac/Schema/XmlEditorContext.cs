// Copyright (c) 2020 Microsoft
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Xsl;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Components;
using MonoDevelop.Components.Extensions;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.TextEditor;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.Xml.Editor
{
	[Export (typeof (ITextViewCreationListener))]
	[ContentType(XmlContentTypeNames.XmlCore)]
	class XmlTextViewCreationListener : ITextViewCreationListener
	{
		public void TextViewCreated (ITextView textView)
		{
			textView.Properties.GetOrCreateSingletonProperty (() => new XmlEditorContext (this, textView));
		}
	}

	class XmlEditorContext
	{
		private readonly XmlTextViewCreationListener factory;
		private readonly ITextView textView;

		XmlSchemaCompletionProvider defaultSchemaCompletionData;
		string defaultNamespacePrefix;
		InferredXmlCompletionProvider inferredCompletionData;
		bool inferenceQueued;

		public XmlEditorContext (XmlTextViewCreationListener factory, ITextView textView)
		{
			this.factory = factory;
			this.textView = textView;
			textView.Closed += TextView_Closed;
			textView.Caret.PositionChanged += Caret_PositionChanged;
			XmlParser = XmlBackgroundParser.GetParser (textView.TextBuffer);

			XmlEditorOptions.XmlFileAssociationChanged += HandleXmlFileAssociationChanged;
			XmlSchemaManager.UserSchemaAdded += UserSchemaAdded;
			XmlSchemaManager.UserSchemaRemoved += UserSchemaRemoved;
			SetDefaultSchema ();
		}

		private void TextView_Closed (object sender, EventArgs e)
		{
			XmlEditorOptions.XmlFileAssociationChanged -= HandleXmlFileAssociationChanged;
			XmlSchemaManager.UserSchemaAdded -= UserSchemaAdded;
			XmlSchemaManager.UserSchemaRemoved -= UserSchemaRemoved;
			ClearTasksForStandaloneXmlFile ();
		}

		void ClearTasksForStandaloneXmlFile ()
		{
			var tasks = IdeServices.TaskService.Errors
				.GetOwnerTasks (this)
				.Where (t => t.WorkspaceObject == null)
				.ToList ();

			if (tasks.Any ())
				IdeServices.TaskService.Errors.RemoveRange (tasks);
		}

		void HandleXmlFileAssociationChanged (object sender, XmlFileAssociationChangedEventArgs e)
		{
			var filename = FilePath;
			if (filename != null && filename.ToString ().EndsWith (e.Extension, StringComparison.Ordinal))
				SetDefaultSchema ();
		}

		void UserSchemaAdded (object source, EventArgs e)
		{
			SetDefaultSchema ();
		}

		void UserSchemaRemoved (object source, EventArgs e)
		{
			SetDefaultSchema ();
		}

		private bool updateScheduled = false;
		private void Caret_PositionChanged (object sender, CaretPositionChangedEventArgs e)
		{
			if (!updateScheduled) {
				updateScheduled = true;
				Task.Delay (500).ContinueWith (_ => {
					updateScheduled = false;
					UpdateOnCaretMove ();
				});
			}
		}

		private void UpdateOnCaretMove ()
		{
			throw new NotImplementedException ();
		}

		public FilePath FilePath => TextBuffer.GetFilePathOrNull ();
		public ITextBuffer TextBuffer => textView.TextBuffer;

		public XmlBackgroundParser XmlParser { get; private set; }

		public XmlParseResult ParseSnapshot(ITextSnapshot snapshot = null)
		{
			snapshot ??= TextBuffer.CurrentSnapshot;
			var result = XmlParser.GetOrProcessAsync (snapshot, CancellationToken.None);
			return result.Result;
		}

		public static XmlEditorContext Get(ITextView textView)
		{
			return textView.Properties.GetProperty<XmlEditorContext> (typeof (XmlEditorContext));
		}

		void SetDefaultSchema ()
		{
			var filename = FilePath;
			if (filename == null) {
				return;
			}

			defaultSchemaCompletionData = XmlSchemaManager.GetSchemaCompletionDataForFileName (filename);
			if (defaultSchemaCompletionData != null)
				inferredCompletionData = null;
			else
				QueueInference ();

			defaultNamespacePrefix = XmlSchemaManager.GetNamespacePrefixForFileName (filename);
		}

		void QueueInference ()
		{
			var parseResult = ParseSnapshot ();
			var doc = parseResult.XDocument;
			int errorCount = parseResult.ParseDiagnostics.Count;
			if (defaultSchemaCompletionData != null || doc == null || doc == null || inferenceQueued)
				return;
			if (inferredCompletionData == null
				// TODO: understand this condition and see if we can match behavior
				//|| (doc.LastWriteTimeUtc - inferredCompletionData.TimeStampUtc).TotalSeconds >= 5
				//	&& errorCount <= inferredCompletionData.ErrorCount
					) {
				inferenceQueued = true;
				System.Threading.ThreadPool.QueueUserWorkItem (delegate {
					try {
						InferredXmlCompletionProvider newData = new InferredXmlCompletionProvider ();
						newData.Populate (doc);
						newData.TimeStampUtc = DateTime.UtcNow;
						newData.ErrorCount = errorCount;
						this.inferenceQueued = false;
						this.inferredCompletionData = newData;
					} catch (Exception ex) {
						Completion.LoggingService.LogWarning ("Unhandled error in XML inference", ex);
					}
				});
			}
		}

		private XmlSpineParser GetSpineParser(SnapshotPoint position = default)
		{
			if (position == default) {
				position = textView.Caret.Position.BufferPosition;
			}

			var spineParser = XmlParser.GetSpineParser (position);
			return spineParser;
		}

		/// <summary>
		/// Gets the XmlSchemaObject that defines the currently selected xml element or attribute.
		/// </summary>
		/// <param name="currentSchemaCompletionData">This is the schema completion data for the schema currently being 
		/// displayed. This can be null if the document is not a schema.</param>
		public XmlSchemaObject GetSchemaObjectSelected ()
		{
			XmlSchemaCompletionProvider currentSchemaCompletionData = XmlSchemaManager.SchemaCompletionDataItems.GetSchemaFromFileName (FilePath);

			// Find element under cursor.
			XmlElementPath path = GetElementPath ();

			//attribute name under cursor, if valid
			string attributeName = null;
			var position = textView.Caret.Position.BufferPosition;
			var spineParser = XmlParser.GetSpineParser (position);

			XAttribute xatt = spineParser.Spine.Peek (0) as XAttribute;
			if (xatt != null) {
				XName xattName = xatt.Name;
				if (spineParser.CurrentState is XmlNameState) {
					xattName = spineParser.GetCompleteName (position.Snapshot);
				}
				attributeName = xattName.FullName;
			}

			// Find schema definition object.
			XmlSchemaCompletionProvider schemaCompletionData = FindSchema(path);

			XmlSchemaObject schemaObject = null;
			if (schemaCompletionData != null) {
				XmlSchemaElement element = schemaCompletionData.FindElement (path);

				schemaObject = element;

				if (element != null) {
					if (!string.IsNullOrEmpty (attributeName)) {
						XmlSchemaAttribute attribute = schemaCompletionData.FindAttribute (element, attributeName);
						if (attribute != null) {
							if (currentSchemaCompletionData != null) {
								schemaObject = GetSchemaObjectReferenced (currentSchemaCompletionData, element, attribute);
							} else {
								schemaObject = attribute;
							}
						}
					}

					return schemaObject;
				}
			}

			return null;
		}

		XmlElementPath GetElementPath ()
		{
			return XmlElementPath.Resolve (
				GetCurrentPath (),
				defaultSchemaCompletionData != null ? defaultSchemaCompletionData.NamespaceUri : null,
				defaultNamespacePrefix ?? ""
			);
		}

		protected List<XObject> GetCurrentPath ()
		{
			var position = textView.Caret.Position.BufferPosition;
			var spineParser = GetSpineParser (position);

			var path = new List<XObject> (spineParser.Spine);

			//remove the root XDocument
			path.RemoveAt (path.Count - 1);

			//complete incomplete XName if present
			if (spineParser.CurrentState is XmlNameState && path[0] is INamedXObject) {
				path[0] = path[0].ShallowCopy ();
				XName completeName = spineParser.GetCompleteName (position.Snapshot);
				((INamedXObject)path[0]).Name = completeName;
			}
			path.Reverse ();
			return path;
		}

		/// <summary>
		/// If the attribute value found references another item in the schema
		/// return this instead of the attribute schema object. For example, if the
		/// user can select the attribute value and the code will work out the schema object pointed to by the ref
		/// or type attribute:
		///
		/// xs:element ref="ref-name"
		/// xs:attribute type="type-name"
		/// </summary>
		/// <returns>
		/// The <paramref name="attribute"/> if no schema object was referenced.
		/// </returns>
		XmlSchemaObject GetSchemaObjectReferenced (XmlSchemaCompletionProvider currentSchemaCompletionData, XmlSchemaElement element, XmlSchemaAttribute attribute)
		{
			XmlSchemaObject schemaObject = null;
			if (IsXmlSchemaNamespace (element)) {
				// Find attribute value.
				//fixme implement
				string attributeValue = "";// XmlParser.GetAttributeValueAtIndex(xml, index);

				if (attributeValue.Length == 0) {
					return attribute;
				}

				if (attribute.Name == "ref") {
					schemaObject = FindSchemaObjectReference (attributeValue, currentSchemaCompletionData, element.Name);
				} else if (attribute.Name == "type") {
					schemaObject = FindSchemaObjectType (attributeValue, currentSchemaCompletionData, element.Name);
				}
			}

			if (schemaObject != null) {
				return schemaObject;
			}

			return attribute;
		}

		/// <summary>
		/// Checks whether the element belongs to the XSD namespace.
		/// </summary>
		static bool IsXmlSchemaNamespace (XmlSchemaElement element)
		{
			XmlQualifiedName qualifiedName = element.QualifiedName;
			if (qualifiedName != null) {
				return XmlSchemaManager.IsXmlSchemaNamespace (qualifiedName.Namespace);
			}

			return false;
		}

		/// <summary>
		/// Attempts to locate the reference name in the specified schema.
		/// </summary>
		/// <param name="name">The reference to look up.</param>
		/// <param name="schemaCompletionData">The schema completion data to use to
		/// find the reference.</param>
		/// <param name="elementName">The element to determine what sort of reference it is
		/// (e.g. group, attribute, element).</param>
		/// <returns><see langword="null"/> if no match can be found.</returns>
		XmlSchemaObject FindSchemaObjectReference (string name, XmlSchemaCompletionProvider schemaCompletionData, string elementName)
		{
			QualifiedName qualifiedName = schemaCompletionData.CreateQualifiedName (name);
			XmlSchemaCompletionProvider qualifiedNameSchema = FindSchema (qualifiedName.Namespace);
			if (qualifiedNameSchema != null) {
				schemaCompletionData = qualifiedNameSchema;
			}
			switch (elementName) {
			case "element":
				return schemaCompletionData.FindElement (qualifiedName);
			case "attribute":
				return schemaCompletionData.FindAttribute (qualifiedName.Name);
			case "group":
				return schemaCompletionData.FindGroup (qualifiedName.Name);
			case "attributeGroup":
				return schemaCompletionData.FindAttributeGroup (qualifiedName.Name);
			}
			return null;
		}

		/// <summary>
		/// Attempts to locate the type name in the specified schema.
		/// </summary>
		/// <param name="name">The type to look up.</param>
		/// <param name="schemaCompletionData">The schema completion data to use to
		/// find the type.</param>
		/// <param name="elementName">The element to determine what sort of type it is
		/// (e.g. group, attribute, element).</param>
		/// <returns><see langword="null"/> if no match can be found.</returns>
		XmlSchemaObject FindSchemaObjectType (string name, XmlSchemaCompletionProvider schemaCompletionData, string elementName)
		{
			QualifiedName qualifiedName = schemaCompletionData.CreateQualifiedName (name);
			XmlSchemaCompletionProvider qualifiedNameSchema = FindSchema (qualifiedName.Namespace);
			if (qualifiedNameSchema != null) {
				schemaCompletionData = qualifiedNameSchema;
			}

			switch (elementName) {
			case "element":
				return schemaCompletionData.FindComplexType (qualifiedName);
			case "attribute":
				return schemaCompletionData.FindSimpleType (qualifiedName.Name);
			}
			return null;
		}

		public XmlSchemaCompletionProvider FindSchemaFromFileName (string fileName)
		{
			return XmlSchemaManager.SchemaCompletionDataItems.GetSchemaFromFileName (fileName);
		}

		public XmlSchemaCompletionProvider FindSchema (string namespaceUri)
		{
			return XmlSchemaManager.SchemaCompletionDataItems[namespaceUri];
		}

		public XmlSchemaCompletionProvider FindSchema (XmlElementPath path)
		{
			return FindSchema (XmlSchemaManager.SchemaCompletionDataItems, path);
		}

		/// <summary>
		/// Finds the schema given the xml element path.
		/// </summary>
		public XmlSchemaCompletionProvider FindSchema (IXmlSchemaCompletionDataCollection schemaCompletionDataItems, XmlElementPath path)
		{
			if (path.Elements.Count > 0) {
				string namespaceUri = path.Elements[0].Namespace;
				if (namespaceUri.Length > 0) {
					return schemaCompletionDataItems[namespaceUri];
				} else if (defaultSchemaCompletionData != null) {
					// Use the default schema namespace if none
					// specified in a xml element path, otherwise
					// we will not find any attribute or element matches
					// later.
					foreach (QualifiedName name in path.Elements) {
						if (name.Namespace.Length == 0) {
							name.Namespace = defaultSchemaCompletionData.NamespaceUri;
						}
					}

					return defaultSchemaCompletionData;
				}
			}

			return null;
		}
	}
}
