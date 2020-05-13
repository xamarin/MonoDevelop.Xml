using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Media;

namespace MonoDevelop.Xml.Editor.Classification
{
	class ClassificationTypeDefinitions
	{
		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlAttributeName)]
		[Name (ClassificationTypeNames.XmlAttributeName)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralAttributeNameFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralAttributeNameFormatDefinition ()
			{
				this.DisplayName = "XML Attribute";
				this.ForegroundColor = Colors.Red;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlAttributeQuotes)]
		[Name (ClassificationTypeNames.XmlAttributeQuotes)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralAttributeQuotesFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralAttributeQuotesFormatDefinition ()
			{
				this.DisplayName = "XML Attribute Quotes";
				this.ForegroundColor = Colors.Black;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlAttributeValue)]
		[Name (ClassificationTypeNames.XmlAttributeValue)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralAttributeValueFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralAttributeValueFormatDefinition ()
			{
				this.DisplayName = "XML Attribute Value";
				this.ForegroundColor = Colors.Blue;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlCDataSection)]
		[Name (ClassificationTypeNames.XmlCDataSection)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralCDataSectionFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralCDataSectionFormatDefinition ()
			{
				this.DisplayName = "XML CData Section";
				this.ForegroundColor = Colors.Gray;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlComment)]
		[Name (ClassificationTypeNames.XmlComment)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralCommentFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralCommentFormatDefinition ()
			{
				this.DisplayName = "XML Comment";
				this.ForegroundColor = Colors.Green;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlDelimiter)]
		[Name (ClassificationTypeNames.XmlDelimiter)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralDelimiterFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralDelimiterFormatDefinition ()
			{
				this.DisplayName = "XML Delimiter";
				this.ForegroundColor = Colors.Blue;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlEntityReference)]
		[Name (ClassificationTypeNames.XmlEntityReference)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralEntityReferenceFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralEntityReferenceFormatDefinition ()
			{
				this.DisplayName = "XML Entity Reference";
				this.ForegroundColor = Colors.Red;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlName)]
		[Name (ClassificationTypeNames.XmlName)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralNameFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralNameFormatDefinition ()
			{
				this.DisplayName = "XML Name";
				this.ForegroundColor = Color.FromRgb (163, 21, 21);
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlProcessingInstruction)]
		[Name (ClassificationTypeNames.XmlProcessingInstruction)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralProcessingInstructionFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralProcessingInstructionFormatDefinition ()
			{
				this.DisplayName = "XML Processing Instruction";
				this.ForegroundColor = Colors.Gray;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = ClassificationTypeNames.XmlText)]
		[Name (ClassificationTypeNames.XmlText)]
		[Order (After = LanguagePriority.NaturalLanguage, Before = LanguagePriority.FormalLanguage)]
		[UserVisible (true)]
		private class XmlLiteralTextFormatDefinition : ClassificationFormatDefinition
		{
			private XmlLiteralTextFormatDefinition ()
			{
				this.DisplayName = "XML Text";
				this.ForegroundColor = Colors.Black;
			}
		}
	}
}
