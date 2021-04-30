namespace SharpDom.Tokenization
{
    public class HtmlParseError
    {
        public static readonly HtmlParseError AbruptClosingOfEmptyComment = new("abrupt-closing-of-empty-comment");
        public static readonly HtmlParseError AbruptDoctypePublicIdentifier = new("abrupt-doctype-public-identifier");
        public static readonly HtmlParseError AbruptDoctypeSystemIdentifier = new("abrupt-doctype-system-identifier");
        public static readonly HtmlParseError AbsenceOfDigitsInNumericCharacterReference = new("absence-of-digits-in-numeric-character-reference");
        public static readonly HtmlParseError CDataInHtmlContent = new("cdata-in-html-content");
        public static readonly HtmlParseError CharacterReferenceOutsideUnicodeRange = new("character-reference-outside-unicode-range");
        public static readonly HtmlParseError ControlCharacterInInputStream = new("control-character-in-input-stream");
        public static readonly HtmlParseError ControlCharacterReference = new("control-character-reference");
        public static readonly HtmlParseError EndTagWithAttributes = new("end-tag-with-attributes");
        public static readonly HtmlParseError DuplicateAttribute = new("duplicate-attribute");
        public static readonly HtmlParseError EndTagWithTrailingSolidus = new("end-tag-with-trailing-solidus");
        public static readonly HtmlParseError EofBeforeTagName = new("eof-before-tag-name");
        public static readonly HtmlParseError EofInCData = new("eof-in-cdata");
        public static readonly HtmlParseError EofInComment = new("eof-in-comment");
        public static readonly HtmlParseError EofInDoctype = new("eof-in-doctype");
        public static readonly HtmlParseError EofInScriptHtmlCommentLikeText = new("eof-in-script-html-comment-like-text");
        public static readonly HtmlParseError EofInTag = new("eof-in-tag");
        public static readonly HtmlParseError IncorrectlyClosedComment = new("incorrectly-closed-comment");
        public static readonly HtmlParseError IncorrectlyOpenedComment = new("incorrectly-opened-comment");
        public static readonly HtmlParseError InvalidCharacterSequenceAfterDoctypeName = new("invalid-character-sequence-after-doctype-name");
        public static readonly HtmlParseError InvalidFirstCharacterOfTagName = new("invalid-first-character-of-tag-name");
        public static readonly HtmlParseError MissingAttributeValue = new("missing-attribute-value");
        public static readonly HtmlParseError MissingDoctypeName = new("missing-doctype-name");
        public static readonly HtmlParseError MissingDoctypePublicIdentifier = new("missing-doctype-public-identifier");
        public static readonly HtmlParseError MissingDoctypeSystemIdentifier = new("missing-doctype-system-identifier");
        public static readonly HtmlParseError MissingEndTagName = new("missing-end-tag-name");
        public static readonly HtmlParseError MissingQuoteBeforeDoctypePublicIdentifier = new("missing-quote-before-doctype-public-identifier");
        public static readonly HtmlParseError MissingQuoteBeforeDoctypeSystemIdentifier = new("missing-quote-before-doctype-system-identifier");
        public static readonly HtmlParseError MissingSemicolonAfterCharacterReference = new("missing-semicolon-after-character-reference");
        public static readonly HtmlParseError MissingWhitespaceAfterDoctypePublicKeyword = new("missing-whitespace-after-doctype-public-keyword");
        public static readonly HtmlParseError MissingWhitespaceAfterDoctypeSystemKeyword = new("missing-whitespace-after-doctype-system-keyword");
        public static readonly HtmlParseError MissingWhitespaceBeforeDoctypeName = new("missing-whitespace-before-doctype-name");
        public static readonly HtmlParseError MissingWhitespaceBetweenAttributes = new("missing-whitespace-between-attributes");
        public static readonly HtmlParseError MissingWhitespaceBetweenDoctypePublicAndSystemIdentifiers = new("missing-whitespace-between-doctype-public-and-system-identifiers");
        public static readonly HtmlParseError NestedComment = new("nested-comment");
        public static readonly HtmlParseError NonCharacterCharacterReference = new("noncharacter-character-reference");
        public static readonly HtmlParseError NonCharacterInInputStream = new("noncharacter-in-input-stream");
        public static readonly HtmlParseError NonVoidHtmlElementStartTagWithTrailingSolidus = new("non-void-html-element-start-tag-with-trailing-solidus");
        public static readonly HtmlParseError NullCharacterReference = new("null-character-reference");
        public static readonly HtmlParseError SurrogateCharacterReference = new("surrogate-character-reference");
        public static readonly HtmlParseError SurrogateInInputStream = new("surrogate-in-input-stream");
        public static readonly HtmlParseError UnexpectedCharacterAfterDoctypeSystemIdentifier = new("unexpected-character-after-doctype-system-identifier");
        public static readonly HtmlParseError UnexpectedCharacterInAttributeName = new("unexpected-character-in-attribute-name");
        public static readonly HtmlParseError UnexpectedCharacterInUnquotedAttributeValue = new("unexpected-character-in-unquoted-attribute-value");
        public static readonly HtmlParseError UnexpectedEqualsSignBeforeAttributeName = new("unexpected-equals-sign-before-attribute-name");
        public static readonly HtmlParseError UnexpectedNullCharacter = new("unexpected-null-character");
        public static readonly HtmlParseError UnexpectedQuestionMarkInsteadOfTagName = new("unexpected-question-mark-instead-of-tag-name");
        public static readonly HtmlParseError UnexpectedSolidusInTag = new("unexpected-solidus-in-tag");
        public static readonly HtmlParseError UnknownNamedCharacterReference = new("unknown-named-character-reference");

        private readonly string _errorCode;
        private HtmlParseError(string errorCode)
        {
            _errorCode = errorCode;
        }

        public override string ToString()
        {
            return _errorCode;
        }
    }
}