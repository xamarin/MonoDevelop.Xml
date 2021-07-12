using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.Xml.Editor.Completion
{
	public class TestCompletionItemSource : XmlCompletionSource
	{
		private ImmutableArray<CompletionItem> sampleItems;
		public TestCompletionItemSource (ITextView textView, XmlSchema schema) : base(textView, schema)
		{
			//sampleItems = ImmutableArray.Create (
			//	new CompletionItem ("Hello", this),
			//	new CompletionItem ("World", this));
		}

		/// <summary>
		/// Converts the element to a complex type if possible.
		/// </summary>
		XmlSchemaComplexType GetElementAsComplexType (XmlSchemaElement element)
		{
			return (element.SchemaType as XmlSchemaComplexType)
				?? XmlSchemaCompletionProvider.FindNamedType (schema, element.SchemaTypeName);
		}

		protected override Task<CompletionContext> GetElementCompletionsAsync (
			IAsyncCompletionSession session,
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			bool includeBracket,
			CancellationToken token
			)
		{
			var node = nodePath.Last () as XElement;
			var xmlPath = XmlElementPath.Resolve (nodePath);
			if (node != null) {
				var list = new XmlSchemaCompletionBuilder (this);
				//var element = FindElement (node.Name.Name);
				var element = FindElement (xmlPath);
				if (element != null)
					GetChildElementCompletionData (list, element, "");
				return Task.FromResult(new CompletionContext (list.GetItems ()));
			}
			return Task.FromResult (CompletionContext.Empty);
		}

		/// <summary>
		/// Gets the child element completion data for the xml element that exists
		/// at the end of the specified path.
		/// </summary>
		public Task<CompletionContext> GetChildElementCompletionDataAsync (IAsyncCompletionSource source, XmlElementPath path, CancellationToken token)
		{
			var builder = new XmlSchemaCompletionBuilder (source, path.Namespaces);
			var element = FindElement (path);
			if (element != null) {
				var last = path.Elements.LastOrDefault ();
				GetChildElementCompletionData (builder, element, last != null ? last.Prefix : "");
			}
			return Task.FromResult(new CompletionContext (builder.GetItems ()));
		}

		void GetChildElementCompletionData (XmlSchemaCompletionBuilder data, XmlSchemaElement element, string prefix)
		{
			var complexType = GetElementAsComplexType (element);
			if (complexType != null)
				GetChildElementCompletionData (data, complexType, prefix);
		}

		void GetChildElementCompletionData (XmlSchemaCompletionBuilder data, XmlSchemaComplexType complexType, string prefix)
		{
			if (complexType.Particle is XmlSchemaSequence sequence) {
				GetChildElementCompletionData (data, sequence.Items, prefix);
				return;
			}
			if (complexType.Particle is XmlSchemaChoice choice) {
				GetChildElementCompletionData (data, choice.Items, prefix);
				return;
			}
			var complexContent = complexType.ContentModel as XmlSchemaComplexContent;
			if (complexContent != null) {
				GetChildElementCompletionData (data, complexContent, prefix);
				return;
			}
			var groupRef = complexType.Particle as XmlSchemaGroupRef;
			if (groupRef != null) {
				GetChildElementCompletionData (data, groupRef, prefix);
				return;
			}
			var all = complexType.Particle as XmlSchemaAll;
			if (all != null) {
				GetChildElementCompletionData (data, all.Items, prefix);
				return;
			}
		}

		void GetChildElementCompletionData (XmlSchemaCompletionBuilder data, XmlSchemaObjectCollection items, string prefix)
		{
			foreach (XmlSchemaObject schemaObject in items) {
				var childElement = schemaObject as XmlSchemaElement;
				if (childElement != null) {
					string name = childElement.Name;
					if (name == null) {
						name = childElement.RefName.Name;
						var element = FindElement (childElement.RefName);
						if (element != null) {
							if (element.IsAbstract) {
								AddSubstitionGroupElements (data, element.QualifiedName, prefix);
							} else {
								data.AddElement (name, prefix, element.Annotation);
							}
						} else {
							data.AddElement (name, prefix, childElement.Annotation);
						}
					} else {
						data.AddElement (name, prefix, childElement.Annotation);
					}
					continue;
				}
				var childSequence = schemaObject as XmlSchemaSequence;
				if (childSequence != null) {
					GetChildElementCompletionData (data, childSequence.Items, prefix);
					continue;
				}
				var childChoice = schemaObject as XmlSchemaChoice;
				if (childChoice != null) {
					GetChildElementCompletionData (data, childChoice.Items, prefix);
					continue;
				}
				var groupRef = schemaObject as XmlSchemaGroupRef;
				if (groupRef != null) {
					GetChildElementCompletionData (data, groupRef, prefix);
					continue;
				}
			}
		}

		void GetChildElementCompletionData (XmlSchemaCompletionBuilder data, XmlSchemaComplexContent complexContent, string prefix)
		{
			var extension = complexContent.Content as XmlSchemaComplexContentExtension;
			if (extension != null) {
				GetChildElementCompletionData (data, extension, prefix);
				return;
			}
			var restriction = complexContent.Content as XmlSchemaComplexContentRestriction;
			if (restriction != null) {
				GetChildElementCompletionData (data, restriction, prefix);
				return;
			}
		}

		void GetChildElementCompletionData (XmlSchemaCompletionBuilder data, XmlSchemaComplexContentExtension extension, string prefix)
		{
			var complexType = XmlSchemaCompletionProvider.FindNamedType (schema, extension.BaseTypeName);
			if (complexType != null)
				GetChildElementCompletionData (data, complexType, prefix);

			if (extension.Particle == null)
				return;

			var sequence = extension.Particle as XmlSchemaSequence;
			if (sequence != null) {
				GetChildElementCompletionData (data, sequence.Items, prefix);
				return;
			}
			var choice = extension.Particle as XmlSchemaChoice;
			if (choice != null) {
				GetChildElementCompletionData (data, choice.Items, prefix);
				return;
			}
			var groupRef = extension.Particle as XmlSchemaGroupRef;
			if (groupRef != null) {
				GetChildElementCompletionData (data, groupRef, prefix);
				return;
			}
		}

		void GetChildElementCompletionData (XmlSchemaCompletionBuilder data, XmlSchemaGroupRef groupRef, string prefix)
		{
			var group = FindGroup (groupRef.RefName.Name);
			if (group == null)
				return;
			var sequence = group.Particle as XmlSchemaSequence;
			if (sequence != null) {
				GetChildElementCompletionData (data, sequence.Items, prefix);
				return;
			}
			var choice = group.Particle as XmlSchemaChoice;
			if (choice != null) {
				GetChildElementCompletionData (data, choice.Items, prefix);
				return;
			}
		}

		void GetChildElementCompletionData (XmlSchemaCompletionBuilder data, XmlSchemaComplexContentRestriction restriction, string prefix)
		{
			if (restriction.Particle == null)
				return;
			var sequence = restriction.Particle as XmlSchemaSequence;
			if (sequence != null) {
				GetChildElementCompletionData (data, sequence.Items, prefix);
				return;
			}
			var choice = restriction.Particle as XmlSchemaChoice;
			if (choice != null) {
				GetChildElementCompletionData (data, choice.Items, prefix);
				return;
			}
			var groupRef = restriction.Particle as XmlSchemaGroupRef;
			if (groupRef != null) {
				GetChildElementCompletionData (data, groupRef, prefix);
				return;
			}
		}

		public static XmlSchemaComplexType FindNamedType (XmlSchema schema, XmlQualifiedName name)
		{
			if (name == null)
				return null;

			foreach (XmlSchemaObject schemaObject in schema.Items) {
				var complexType = schemaObject as XmlSchemaComplexType;
				if (complexType != null && complexType.QualifiedName == name)
					return complexType;
			}

			// Try included schemas.
			foreach (XmlSchemaExternal external in schema.Includes) {
				var include = external as XmlSchemaInclude;
				if (include != null && include.Schema != null) {
					var matchedComplexType = FindNamedType (include.Schema, name);
					if (matchedComplexType != null)
						return matchedComplexType;
				}
			}

			return null;
		}

		public XmlSchemaElement FindElement (string name)
		{
			foreach (XmlSchemaElement element in schema.Items)
				if (element.Name == name)
					return element;

			LoggingService.LogDebug ("XmlSchemaDataObject did not find element '{0}' in the schema", name);
			return null;
		}

		/// <summary>
		/// Finds an element in the schema.
		/// </summary>
		/// <remarks>
		/// Only looks at the elements that are defined in the 
		/// root of the schema so it will not find any elements
		/// that are defined inside any complex types.
		/// </remarks>
		public XmlSchemaElement FindElement (XmlQualifiedName name)
		{
			XmlSchemaElement matchedElement = null;
			foreach (XmlSchemaElement element in schema.Items) {
				//if (name.Equals (element.QualifiedName)) {
				//	matchedElement = element;
				//	break;
				//}
				if (name.Name == element.Name) {
					matchedElement = element;
					break;
				}
			}

			return matchedElement;
		}

		/// <summary>
		/// Finds an element in the schema.
		/// </summary>
		/// <remarks>
		/// Only looks at the elements that are defined in the 
		/// root of the schema so it will not find any elements
		/// that are defined inside any complex types.
		/// </remarks>
		public XmlSchemaElement FindElement (QualifiedName name)
		{
			foreach (XmlSchemaElement element in schema.Elements.Values) {
				if (name.Equals (element.QualifiedName)) {
					return element;
				}
			}
			LoggingService.LogDebug ("XmlSchemaDataObject did not find element '{0}' in the schema", name.Name);
			return null;
		}

		/// <summary>
		/// Finds the element that exists at the specified path.
		/// </summary>
		/// <remarks>This method is not used when generating completion data,
		/// but is a useful method when locating an element so we can jump
		/// to its schema definition.</remarks>
		/// <returns><see langword="null"/> if no element can be found.</returns>
		public XmlSchemaElement FindElement (XmlElementPath path)
		{
			XmlSchemaElement element = null;
			for (int i = 0; i < path.Elements.Count; ++i) {
				QualifiedName name = path.Elements[i];
				if (i == 0) {
					// Look for root element.
					element = FindElement (name.Name);
					if (element == null) {
						break;
					}
				} else {
					element = FindChildElement (element, name);
					if (element == null) {
						break;
					}
				}
			}
			return element;
		}

		/// <summary>
		/// Finds the element in the collection of schema objects.
		/// </summary>
		XmlSchemaElement FindElement (XmlSchemaObjectCollection items, QualifiedName name)
		{
			XmlSchemaElement matchedElement = null;

			foreach (XmlSchemaObject schemaObject in items) {
				var element = schemaObject as XmlSchemaElement;
				var sequence = schemaObject as XmlSchemaSequence;
				var choice = schemaObject as XmlSchemaChoice;
				var groupRef = schemaObject as XmlSchemaGroupRef;

				if (element != null) {
					if (element.Name != null) {
						if (name.Name == element.Name) {
							return element;
						}
					} else if (element.RefName != null) {
						if (name.Name == element.RefName.Name) {
							matchedElement = FindElement (element.RefName);
						} else {
							var abstractElement = FindElement (element.RefName);
							if (abstractElement != null && abstractElement.IsAbstract) {
								matchedElement = FindSubstitutionGroupElement (abstractElement.QualifiedName, name);
							}
						}
					}
				} else if (sequence != null) {
					matchedElement = FindElement (sequence.Items, name);
				} else if (choice != null) {
					matchedElement = FindElement (choice.Items, name);
				} else if (groupRef != null) {
					matchedElement = FindElement (groupRef, name);
				}

				if (matchedElement != null)
					return matchedElement;
			}

			return null;
		}

		XmlSchemaElement FindElement (XmlSchemaGroupRef groupRef, QualifiedName name)
		{
			var group = FindGroup (groupRef.RefName.Name);
			if (group == null)
				return null;

			var sequence = group.Particle as XmlSchemaSequence;
			if (sequence != null)
				return FindElement (sequence.Items, name);
			var choice = group.Particle as XmlSchemaChoice;
			if (choice != null)
				return FindElement (choice.Items, name);

			return null;
		}

		/// <summary>
		/// Finds an element that matches the specified <paramref name="name"/>
		/// from the children of the given <paramref name="element"/>.
		/// </summary>
		XmlSchemaElement FindChildElement (XmlSchemaElement element, QualifiedName name)
		{
			var complexType = GetElementAsComplexType (element);
			if (complexType != null)
				return FindChildElement (complexType, name);
			return null;
		}

		XmlSchemaElement FindChildElement (XmlSchemaComplexType complexType, QualifiedName name)
		{
			var sequence = complexType.Particle as XmlSchemaSequence;
			if (sequence != null)
				return FindElement (sequence.Items, name);

			var choice = complexType.Particle as XmlSchemaChoice;
			if (choice != null)
				return FindElement (choice.Items, name);

			var complexContent = complexType.ContentModel as XmlSchemaComplexContent;
			if (complexContent != null) {
				var extension = complexContent.Content as XmlSchemaComplexContentExtension;
				if (extension != null)
					return FindChildElement (extension, name);
				var restriction = complexContent.Content as XmlSchemaComplexContentRestriction;
				if (restriction != null)
					return FindChildElement (restriction, name);
			}

			var groupRef = complexType.Particle as XmlSchemaGroupRef;
			if (groupRef != null)
				return FindElement (groupRef, name);

			var all = complexType.Particle as XmlSchemaAll;
			if (all != null)
				return FindElement (all.Items, name);

			return null;
		}

		/// <summary>
		/// Finds the named child element contained in the extension element.
		/// </summary>
		XmlSchemaElement FindChildElement (XmlSchemaComplexContentExtension extension, QualifiedName name)
		{
			var complexType = FindNamedType (schema, extension.BaseTypeName);
			if (complexType == null)
				return null;

			var matchedElement = FindChildElement (complexType, name);
			if (matchedElement != null)
				return matchedElement;

			var sequence = extension.Particle as XmlSchemaSequence;
			if (sequence != null)
				return FindElement (sequence.Items, name);

			var choice = extension.Particle as XmlSchemaChoice;
			if (choice != null)
				return FindElement (choice.Items, name);

			var groupRef = extension.Particle as XmlSchemaGroupRef;
			if (groupRef != null)
				return FindElement (groupRef, name);

			return null;
		}

		/// <summary>
		/// Finds the named child element contained in the restriction element.
		/// </summary>
		XmlSchemaElement FindChildElement (XmlSchemaComplexContentRestriction restriction, QualifiedName name)
		{
			var sequence = restriction.Particle as XmlSchemaSequence;
			if (sequence != null)
				return FindElement (sequence.Items, name);

			var groupRef = restriction.Particle as XmlSchemaGroupRef;
			if (groupRef != null)
				return FindElement (groupRef, name);

			return null;
		}

		/// <summary>
		/// Finds the schema group with the specified name.
		/// </summary>
		public XmlSchemaGroup FindGroup (string name)
		{
			if (name != null) {
				foreach (XmlSchemaObject schemaObject in schema.Groups.Values) {
					var group = schemaObject as XmlSchemaGroup;
					if (group != null && group.Name == name)
						return group;
				}
			}
			return null;
		}

		/// <summary>
		/// Adds any elements that have the specified substitution group.
		/// </summary>
		void AddSubstitionGroupElements (XmlSchemaCompletionBuilder data, XmlQualifiedName group, string prefix)
		{
			foreach (XmlSchemaElement element in schema.Elements.Values)
				if (element.SubstitutionGroup == group)
					data.AddElement (element.Name, prefix, element.Annotation);
		}

		/// <summary>
		/// Looks for the substitution group element of the specified name.
		/// </summary>
		XmlSchemaElement FindSubstitutionGroupElement (XmlQualifiedName group, QualifiedName name)
		{
			foreach (XmlSchemaElement element in schema.Elements.Values)
				if (element.SubstitutionGroup == group && element.Name != null && element.Name == name.Name)
					return element;

			return null;
		}
	}
}
