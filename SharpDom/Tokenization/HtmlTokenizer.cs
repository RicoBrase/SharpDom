using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SharpDom.Infra.Unicode;
using SharpDom.Parsing;
using SharpDom.Tokenization.Tokens;
using SharpDom.Utils;

namespace SharpDom.Tokenization
{
    public class HtmlTokenizer
    {
        private readonly string _input;
        private readonly Queue<HtmlToken> _tokens = new();
        private readonly Queue<HtmlParseError> _errors = new();

        private int _cursor;
        private int _characterReferenceCode;
        private bool _reconsume;
        private bool _isEof;
        
        private readonly bool _debug;

        private HtmlTokenizerState _state = HtmlTokenizerState.Data;
        private HtmlTokenizerState _returnState;

        private readonly StringBuilder _temporaryBuffer = new();
        
        private Optional<char> _nextInputChar = Optional<char>.Empty();
        private Optional<char> _currentInputChar = Optional<char>.Empty();

        private HtmlToken _currentToken;

        public HtmlTokenizer(string input, bool debugMode = false)
        {
            _input = InputStreamPreprocessor.PreprocessInputStream(input);
            _debug = debugMode;
        }

        internal HtmlTokenizer(string input, bool debugMode = false,
            HtmlTokenizerState initialState = HtmlTokenizerState.Data)
        : this(input, debugMode)
        {
            _state = initialState;
            if(_debug) Console.WriteLine($"[DBG] Starting with State: {Enum.GetName(initialState)}");
        }

        public HtmlTokenizationResult Run()
        {
            ConsumeNextInputChar();
            while (!_isEof)
            {
                RunTokenizationStep();
            }

            var tokenList = _tokens.ToArray();
            var processedTokens = new List<HtmlToken>();
            var i = 0;
            
            while (i < tokenList.Length)
            {
                if(_debug) Console.WriteLine($"[DBG] TokenProcessing: {i}");
                if (i > 0 && processedTokens.Count > 0)
                {
                    var previousToken = processedTokens[^1];

                    if (previousToken.Type == tokenList[i].Type && previousToken.Type == HtmlTokenType.Character)
                    {
                        ((HtmlCharacterToken) previousToken).Data += ((HtmlCharacterToken) tokenList[i]).Data;
                        i++;
                        continue;
                    }
                    if (previousToken.Type == tokenList[i].Type && previousToken.Type == HtmlTokenType.Comment)
                    {
                        ((HtmlCommentToken) previousToken).Data += ((HtmlCommentToken) tokenList[i]).Data;
                        i++;
                        continue;
                    }
                }

                processedTokens.Add(tokenList[i]);
                i++;
            }
            
            return new HtmlTokenizationResult
            {
                Tokens = processedTokens,
                Errors = _errors
            };
        }

        private void RunTokenizationStep()
        {
            char currChar;
            switch (_state)
            {
                case HtmlTokenizerState.Data:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '&':
                                _returnState = HtmlTokenizerState.Data;
                                SwitchToState(HtmlTokenizerState.CharacterReference);
                                break;
                            case '<':
                                SwitchToState(HtmlTokenizerState.TagOpen);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                CreateToken(new HtmlCharacterToken {Data = currChar.ToString()});
                                EmitCurrentToken();
                                break;
                            default:
                                CreateToken(new HtmlCharacterToken {Data = currChar.ToString()});
                                EmitCurrentToken();
                                break;
                        }

                        return;
                    }
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.ScriptData:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '<':
                                SwitchToState(HtmlTokenizerState.ScriptDataLessThanSign);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                CreateToken(new HtmlCharacterToken {Data = Codepoint.REPLACEMENT_CHARACTER.ToString()});
                                EmitCurrentToken();
                                break;
                            default:
                                CreateToken(new HtmlCharacterToken {Data = currChar.ToString()});
                                EmitCurrentToken();
                                break;
                        }
                        return;
                    }
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.TagOpen:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '!':
                                SwitchToState(HtmlTokenizerState.MarkupDeclarationOpen);
                                break;
                            case '/':
                                SwitchToState(HtmlTokenizerState.EndTagOpen);
                                break;
                            case '?':
                                ParseError(HtmlParseError.UnexpectedQuestionMarkInsteadOfTagName);
                                CreateToken(new HtmlCommentToken {Data = ""});
                                ReconsumeInState(HtmlTokenizerState.BogusComment);
                                break;
                            default:
                                var cp = Codepoint.Get(currChar);
                                if (cp.IsAsciiAlpha())
                                {
                                    CreateToken(new HtmlStartTagToken {TagName = ""});
                                    ReconsumeInState(HtmlTokenizerState.TagName);
                                    return;
                                }
                                ParseError(HtmlParseError.InvalidFirstCharacterOfTagName);
                                CreateToken(new HtmlCharacterToken {Data = '<'.ToString()});
                                EmitCurrentToken();
                                ReconsumeInState(HtmlTokenizerState.Data);
                                break;
                        }

                        return;
                    }
                    CreateToken(new HtmlCharacterToken {Data = '<'.ToString()});
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.EndTagOpen:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '>':
                                ParseError(HtmlParseError.MissingEndTagName);
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                if (Codepoint.Get(currChar).IsAsciiAlpha())
                                {
                                    CreateToken(new HtmlEndTagToken {TagName = ""});
                                    ReconsumeInState(HtmlTokenizerState.TagName);
                                    return;
                                }
                                ParseError(HtmlParseError.InvalidFirstCharacterOfTagName);
                                CreateToken(new HtmlCommentToken {Data = ""});
                                ReconsumeInState(HtmlTokenizerState.BogusComment);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofBeforeTagName);
                    CreateToken(new HtmlCharacterToken {Data = '<'.ToString()});
                    EmitCurrentToken();
                    CreateToken(new HtmlCharacterToken {Data = '/'.ToString()});
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.TagName:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.BeforeAttributeName);
                                break;
                            case '/':
                                SwitchToState(HtmlTokenizerState.SelfClosingStartTag);
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlTagToken) _currentToken).TagName += Codepoint.REPLACEMENT_CHARACTER;
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).TagName +=
                                    Codepoint.Get(currChar).IsAsciiUpperAlpha()
                                        ? char.ToLower(currChar)
                                        : currChar;
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInTag);
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.BeforeAttributeName:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '/':
                            case '>':
                                ReconsumeInState(HtmlTokenizerState.AfterAttributeName);
                                break;
                            case '=':
                                ParseError(HtmlParseError.UnexpectedEqualsSignBeforeAttributeName);
                                ((HtmlTagToken) _currentToken).CurrentAttribute.KeyBuilder.Append(currChar);
                                SwitchToState(HtmlTokenizerState.AttributeName);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).StartNewAttribute();
                                ReconsumeInState(HtmlTokenizerState.AttributeName);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.AfterAttributeName);
                    break;
                
                case HtmlTokenizerState.AttributeName:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                            case '/':
                            case '>':
                                CheckTagAttributes();
                                ReconsumeInState(HtmlTokenizerState.AfterAttributeName);
                                break;
                            case '=':
                                CheckTagAttributes();
                                SwitchToState(HtmlTokenizerState.BeforeAttributeValue);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlTagToken) _currentToken).CurrentAttribute.KeyBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            case '"':
                            case Codepoint.APOSTROPHE:
                            case '<':
                                ParseError(HtmlParseError.UnexpectedCharacterInAttributeName);
                                ((HtmlTagToken) _currentToken).CurrentAttribute.KeyBuilder.Append(currChar);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.KeyBuilder.Append(Codepoint.Get(currChar).IsAsciiUpperAlpha() ? char.ToLower(currChar) : currChar);
                                break;
                        }
                        return;
                    }
                    CheckTagAttributes();
                    ReconsumeInState(HtmlTokenizerState.AfterAttributeName);
                    break;
                
                case HtmlTokenizerState.AfterAttributeName:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '/':
                                SwitchToState(HtmlTokenizerState.SelfClosingStartTag);
                                break;
                            case '=':
                                SwitchToState(HtmlTokenizerState.BeforeAttributeValue);
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).StartNewAttribute();
                                ReconsumeInState(HtmlTokenizerState.AttributeName);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInTag);
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.BeforeAttributeValue:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '"':
                                SwitchToState(HtmlTokenizerState.AttributeValueDoubleQuoted);
                                break;
                            case Codepoint.APOSTROPHE:
                                SwitchToState(HtmlTokenizerState.AttributeValueSingleQuoted);
                                break;
                            case '>':
                                ParseError(HtmlParseError.MissingAttributeValue);
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ReconsumeInState(HtmlTokenizerState.AttributeValueUnquoted);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.AttributeValueUnquoted);
                    break;
                
                case HtmlTokenizerState.AttributeValueDoubleQuoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '"':
                                SwitchToState(HtmlTokenizerState.AfterAttributeValueQuoted);
                                break;
                            case '&':
                                _returnState = HtmlTokenizerState.AttributeValueDoubleQuoted;
                                SwitchToState(HtmlTokenizerState.CharacterReference);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInTag);
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AttributeValueSingleQuoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '\'':
                                SwitchToState(HtmlTokenizerState.AfterAttributeValueQuoted);
                                break;
                            case '&':
                                _returnState = HtmlTokenizerState.AttributeValueSingleQuoted;
                                SwitchToState(HtmlTokenizerState.CharacterReference);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInTag);
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AttributeValueUnquoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.BeforeAttributeName);
                                break;
                            case '&':
                                _returnState = HtmlTokenizerState.AttributeValueUnquoted;
                                SwitchToState(HtmlTokenizerState.CharacterReference);
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            case '"':
                            case Codepoint.APOSTROPHE:
                            case '<':
                            case '=':
                            case Codepoint.GRAVE_ACCENT:
                                ParseError(HtmlParseError.UnexpectedCharacterInUnquotedAttributeValue);
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInTag);
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AfterAttributeValueQuoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)  
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.BeforeAttributeName);
                                break;
                            case '/':
                                SwitchToState(HtmlTokenizerState.SelfClosingStartTag);
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingWhitespaceBetweenAttributes);
                                ReconsumeInState(HtmlTokenizerState.BeforeAttributeName);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInTag);
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.SelfClosingStartTag:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '>':
                                ((HtmlTagToken) _currentToken).SelfClosing = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ParseError(HtmlParseError.UnexpectedSolidusInTag);
                                ReconsumeInState(HtmlTokenizerState.BeforeAttributeName);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInTag);
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.BogusComment:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlCommentToken) _currentToken).Data += Codepoint.REPLACEMENT_CHARACTER;
                                break;
                            default:
                                ((HtmlCommentToken) _currentToken).Data += currChar;
                                break;
                        }
                        return;
                    }
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.MarkupDeclarationOpen:
                    if (CheckForStringCaseSensitive("--"))
                    {
                        ConsumeString("--");
                        CreateToken(new HtmlCommentToken {Data = ""});
                        SwitchToState(HtmlTokenizerState.CommentStart);
                        return;
                    }

                    if (CheckForStringCaseInsensitive("DOCTYPE"))
                    {
                        ConsumeString("DOCTYPE");
                        SwitchToState(HtmlTokenizerState.Doctype);
                        return;
                    }
                    ParseError(HtmlParseError.IncorrectlyOpenedComment);
                    CreateToken(new HtmlCommentToken { Data = "" });
                    SwitchToState(HtmlTokenizerState.BogusComment);
                    break;
                
                case HtmlTokenizerState.CommentStart:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '-':
                                SwitchToState(HtmlTokenizerState.CommentStartDash);
                                break;
                            case '>':
                                ParseError(HtmlParseError.AbruptClosingOfEmptyComment);
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ReconsumeInState(HtmlTokenizerState.Comment);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.Comment);
                    break;
                
                case HtmlTokenizerState.CommentStartDash:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '-':
                                SwitchToState(HtmlTokenizerState.CommentEnd);
                                break;
                            case '>':
                                ParseError(HtmlParseError.AbruptClosingOfEmptyComment);
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ((HtmlCommentToken) _currentToken).Data += '-';
                                ReconsumeInState(HtmlTokenizerState.Comment);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInComment);
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.Comment:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '<':
                                ((HtmlCommentToken) _currentToken).Data += currChar;
                                SwitchToState(HtmlTokenizerState.CommentLessThanSign);
                                break;
                            case '-':
                                SwitchToState(HtmlTokenizerState.CommentEndDash);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlCommentToken) _currentToken).Data += Codepoint.REPLACEMENT_CHARACTER;
                                break;
                            default:
                                ((HtmlCommentToken) _currentToken).Data += currChar;
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInComment);
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.CommentLessThanSign:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '!':
                                ((HtmlCommentToken) _currentToken).Data += currChar;
                                SwitchToState(HtmlTokenizerState.CommentLessThanSignBang);
                                break;
                            case '<':
                                ((HtmlCommentToken) _currentToken).Data += currChar;
                                break;
                            default:
                                ReconsumeInState(HtmlTokenizerState.Comment);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.Comment);
                    break;
                
                case HtmlTokenizerState.CommentLessThanSignBang:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '-':
                                SwitchToState(HtmlTokenizerState.CommentLessThanSignBangDash);
                                break;
                            default:
                                ReconsumeInState(HtmlTokenizerState.Comment);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.Comment);
                    break;
                
                case HtmlTokenizerState.CommentLessThanSignBangDash:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '-':
                                SwitchToState(HtmlTokenizerState.CommentLessThanSignBangDashDash);
                                break;
                            default:
                                ReconsumeInState(HtmlTokenizerState.CommentEndDash);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.CommentEndDash);
                    break;
                
                case HtmlTokenizerState.CommentLessThanSignBangDashDash:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '>':
                                ReconsumeInState(HtmlTokenizerState.CommentEnd);
                                break;
                            default:
                                ParseError(HtmlParseError.NestedComment);
                                ReconsumeInState(HtmlTokenizerState.CommentEnd);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.CommentEnd);
                    break;
                
                case HtmlTokenizerState.CommentEndDash:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '-':
                                SwitchToState(HtmlTokenizerState.CommentEnd);
                                break;
                            default:
                                ((HtmlCommentToken) _currentToken).Data += '-';
                                ReconsumeInState(HtmlTokenizerState.Comment);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInComment);
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.CommentEnd:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case '!':
                                SwitchToState(HtmlTokenizerState.CommentEndBang);
                                break;
                            case '-':
                                ((HtmlCommentToken) _currentToken).Data += '-';
                                break;
                            default:
                                ((HtmlCommentToken) _currentToken).Data += "--";
                                ReconsumeInState(HtmlTokenizerState.Comment);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInComment);
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;

                case HtmlTokenizerState.Doctype:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.BeforeDoctypeName);
                                break;
                            
                            case '>':
                                ReconsumeInState(HtmlTokenizerState.BeforeDoctypeName);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingWhitespaceBeforeDoctypeName);
                                ReconsumeInState(HtmlTokenizerState.BeforeDoctypeName);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    CreateToken(new HtmlDoctypeToken
                    {
                        ForceQuirks = true
                    });
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.BeforeDoctypeName:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        Console.WriteLine($"[DBG] BeforeDoctypeName_1 | currChar = {currChar}");
                        
                        var cp = Codepoint.Get(currChar);
                        if (cp.IsAsciiUpperAlpha())
                        {
                            CreateToken(new HtmlDoctypeToken
                            {
                                Name = Optional<string>.Of(char.ToLower(currChar).ToString())
                            });
                            SwitchToState(HtmlTokenizerState.DoctypeName);
                            break;
                        }
                        
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case Codepoint.NULL:
                                CreateToken(new HtmlDoctypeToken
                                {
                                    Name = Optional<string>.Of(Codepoint.REPLACEMENT_CHARACTER.ToString())
                                });
                                SwitchToState(HtmlTokenizerState.DoctypeName);
                                break;
                            case '>':
                                ParseError(HtmlParseError.MissingDoctypeName);
                                CreateToken(new HtmlDoctypeToken
                                {
                                    ForceQuirks = true
                                });
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                Console.WriteLine($"[DBG] BeforeDoctypeName_2 | currChar = {currChar}");
                                CreateToken(new HtmlDoctypeToken
                                {
                                    Name = Optional<string>.Of(currChar.ToString())
                                });
                                SwitchToState(HtmlTokenizerState.DoctypeName);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    CreateToken(new HtmlDoctypeToken
                    {
                        ForceQuirks = true
                    });
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.DoctypeName:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.AfterDoctypeName);
                                break;
                            
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            
                            case Codepoint.NULL:
                                AppendCharToDoctypeTokenName(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            
                            default:
                                var cp = Codepoint.Get(currChar);
                                AppendCharToDoctypeTokenName(cp.IsAsciiUpperAlpha()
                                    ? char.ToLower(currChar)
                                    : currChar);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AfterDoctypeName:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                if (CheckForStringCaseInsensitiveFromCurrentInputCharacter("PUBLIC"))
                                {
                                    ConsumeStringFromCurrentInputCharacter("PUBLIC");
                                    SwitchToState(HtmlTokenizerState.AfterDoctypePublicKeyword);
                                    return;
                                }
                                else if (CheckForStringCaseInsensitiveFromCurrentInputCharacter("SYSTEM"))
                                {
                                    ConsumeStringFromCurrentInputCharacter("SYSTEM");
                                    SwitchToState(HtmlTokenizerState.AfterDoctypeSystemKeyword);
                                    return;
                                }

                                ParseError(HtmlParseError.InvalidCharacterSequenceAfterDoctypeName);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AfterDoctypePublicKeyword:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.BeforeDoctypePublicIdentifier);
                                break;
                            case '"':
                                ParseError(HtmlParseError.MissingWhitespaceAfterDoctypePublicKeyword);
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypePublicIdentifierDoubleQuoted);
                                break;
                            case Codepoint.APOSTROPHE:
                                ParseError(HtmlParseError.MissingWhitespaceAfterDoctypePublicKeyword);
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypePublicIdentifierSingleQuoted);
                                break;
                            case '>':
                                ParseError(HtmlParseError.MissingDoctypePublicIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingQuoteBeforeDoctypePublicIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;

                case HtmlTokenizerState.BeforeDoctypePublicIdentifier:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '"':
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypePublicIdentifierDoubleQuoted);
                                break;
                            case Codepoint.APOSTROPHE:
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypePublicIdentifierSingleQuoted);
                                break;
                            case '>':
                                ParseError(HtmlParseError.MissingDoctypePublicIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingQuoteBeforeDoctypePublicIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.DoctypePublicIdentifierDoubleQuoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        string doctypeTokenPubId;
                        
                        switch (currChar)
                        {
                            case '"':
                                SwitchToState(HtmlTokenizerState.AfterDoctypePublicIdentifier);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = ((HtmlDoctypeToken) _currentToken).PublicIdentifier.TryGet(
                                    out doctypeTokenPubId)
                                    ? Optional<string>.Of(doctypeTokenPubId + Codepoint.REPLACEMENT_CHARACTER)
                                    : Optional<string>.Of(Codepoint.REPLACEMENT_CHARACTER.ToString());
                                break;
                            case '>':
                                ParseError(HtmlParseError.AbruptDoctypePublicIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = ((HtmlDoctypeToken) _currentToken).PublicIdentifier.TryGet(
                                    out doctypeTokenPubId)
                                    ? Optional<string>.Of(doctypeTokenPubId + currChar)
                                    : Optional<string>.Of(currChar.ToString());
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.DoctypePublicIdentifierSingleQuoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        string doctypeTokenPubId;
                        
                        switch (currChar)
                        {
                            case Codepoint.APOSTROPHE:
                                SwitchToState(HtmlTokenizerState.AfterDoctypePublicIdentifier);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = ((HtmlDoctypeToken) _currentToken).PublicIdentifier.TryGet(
                                    out doctypeTokenPubId)
                                    ? Optional<string>.Of(doctypeTokenPubId + Codepoint.REPLACEMENT_CHARACTER)
                                    : Optional<string>.Of(Codepoint.REPLACEMENT_CHARACTER.ToString());
                                break;
                            case '>':
                                ParseError(HtmlParseError.AbruptDoctypePublicIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ((HtmlDoctypeToken)_currentToken).PublicIdentifier = ((HtmlDoctypeToken) _currentToken).PublicIdentifier.TryGet(
                                    out doctypeTokenPubId)
                                    ? Optional<string>.Of(doctypeTokenPubId + currChar)
                                    : Optional<string>.Of(currChar.ToString());
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AfterDoctypePublicIdentifier:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.BetweenDoctypePublicAndSystemIdentifiers);
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case '"':
                                ParseError(HtmlParseError.MissingWhitespaceBetweenDoctypePublicAndSystemIdentifiers);
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierDoubleQuoted);
                                break;
                            case Codepoint.APOSTROPHE:
                                ParseError(HtmlParseError.MissingWhitespaceBetweenDoctypePublicAndSystemIdentifiers);
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierSingleQuoted);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingQuoteBeforeDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.BetweenDoctypePublicAndSystemIdentifiers:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case '"':
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierDoubleQuoted);
                                break;
                            case Codepoint.APOSTROPHE:
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierSingleQuoted);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingQuoteBeforeDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AfterDoctypeSystemKeyword:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                SwitchToState(HtmlTokenizerState.BeforeDoctypeSystemIdentifier);
                                break;
                            case '"':
                                ParseError(HtmlParseError.MissingWhitespaceAfterDoctypeSystemKeyword);
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierDoubleQuoted);
                                break;
                            case Codepoint.APOSTROPHE:
                                ParseError(HtmlParseError.MissingWhitespaceAfterDoctypeSystemKeyword);
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierSingleQuoted);
                                break;
                            case '>':
                                ParseError(HtmlParseError.MissingDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingQuoteBeforeDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;

                case HtmlTokenizerState.BeforeDoctypeSystemIdentifier:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '"':
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierDoubleQuoted);
                                break;
                            case Codepoint.APOSTROPHE:
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = Optional<string>.Of("");
                                SwitchToState(HtmlTokenizerState.DoctypeSystemIdentifierSingleQuoted);
                                break;
                            case '>':
                                ParseError(HtmlParseError.MissingDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ParseError(HtmlParseError.MissingQuoteBeforeDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.DoctypeSystemIdentifierDoubleQuoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        string doctypeTokenSysId;
                        
                        switch (currChar)
                        {
                            case '"':
                                SwitchToState(HtmlTokenizerState.AfterDoctypeSystemIdentifier);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = ((HtmlDoctypeToken) _currentToken).SystemIdentifier.TryGet(
                                    out doctypeTokenSysId)
                                    ? Optional<string>.Of(doctypeTokenSysId + Codepoint.REPLACEMENT_CHARACTER)
                                    : Optional<string>.Of(Codepoint.REPLACEMENT_CHARACTER.ToString());
                                break;
                            case '>':
                                ParseError(HtmlParseError.AbruptDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = ((HtmlDoctypeToken) _currentToken).SystemIdentifier.TryGet(
                                    out doctypeTokenSysId)
                                    ? Optional<string>.Of(doctypeTokenSysId + currChar)
                                    : Optional<string>.Of(currChar.ToString());
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.DoctypeSystemIdentifierSingleQuoted:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        string doctypeTokenSysId;
                        
                        switch (currChar)
                        {
                            case Codepoint.APOSTROPHE:
                                SwitchToState(HtmlTokenizerState.AfterDoctypeSystemIdentifier);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = ((HtmlDoctypeToken) _currentToken).SystemIdentifier.TryGet(
                                    out doctypeTokenSysId)
                                    ? Optional<string>.Of(doctypeTokenSysId + Codepoint.REPLACEMENT_CHARACTER)
                                    : Optional<string>.Of(Codepoint.REPLACEMENT_CHARACTER.ToString());
                                break;
                            case '>':
                                ParseError(HtmlParseError.AbruptDoctypeSystemIdentifier);
                                ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ((HtmlDoctypeToken)_currentToken).SystemIdentifier = ((HtmlDoctypeToken) _currentToken).SystemIdentifier.TryGet(
                                    out doctypeTokenSysId)
                                    ? Optional<string>.Of(doctypeTokenSysId + currChar)
                                    : Optional<string>.Of(currChar.ToString());
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.AfterDoctypeSystemIdentifier:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)       
                        {
                            case Codepoint.TAB:
                            case Codepoint.LF:
                            case Codepoint.FF:
                            case Codepoint.SPACE:
                                IgnoreCharacter();
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ParseError(HtmlParseError.UnexpectedCharacterAfterDoctypeSystemIdentifier);
                                ReconsumeInState(HtmlTokenizerState.BogusDoctype);
                                break;
                        }
                        return;
                    }
                    ParseError(HtmlParseError.EofInDoctype);
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.BogusDoctype:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case Codepoint.NULL:
                                ParseError(HtmlParseError.UnexpectedNullCharacter);
                                IgnoreCharacter();
                                break;
                            default:
                                IgnoreCharacter();
                                break;
                        }
                        return;
                    }
                    EmitCurrentToken();
                    EmitEndOfFileToken();
                    break;

                case HtmlTokenizerState.CharacterReference:
                    _temporaryBuffer.Clear();
                    _temporaryBuffer.Append('&');
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '#':
                                _temporaryBuffer.Append(currChar);
                                SwitchToState(HtmlTokenizerState.NumericCharacterReference);
                                break;
                            default:
                                if (Codepoint.Get(currChar).IsAsciiAlphanumeric())
                                {
                                    ReconsumeInState(HtmlTokenizerState.NamedCharacterReference);
                                    return;
                                }
                                FlushCodepointsConsumedAsCharacterReference();
                                ReconsumeInState(_returnState);
                                break;
                        }
                        return;
                    }
                    FlushCodepointsConsumedAsCharacterReference();
                    ReconsumeInState(_returnState);
                    break;
                
                case HtmlTokenizerState.NamedCharacterReference:
                    string possibleMatch = null;
                    var cursorPos = 0;

                    while (true)
                    {
                        ConsumeNextInputChar();
                        int countPossibleReferences;
                        if (!_currentInputChar.TryGet(out currChar))
                        {
                            if (possibleMatch == null) break;

                            countPossibleReferences = 1;
                        }
                        else
                        {
                            _temporaryBuffer.Append(currChar);
                            countPossibleReferences =
                                NamedCharacterReference.GetAllPossibleNamedCharacterReferences(
                                    _temporaryBuffer.ToString());
                            if (NamedCharacterReference.IsNamedCharacterReference(_temporaryBuffer.ToString()))
                            {
                                possibleMatch = _temporaryBuffer.ToString();
                                cursorPos = _cursor - 1;
                            }
                        }

                        if (countPossibleReferences != 1) continue;
                        // MATCHED

                        if (possibleMatch is not null)
                        {
                            _temporaryBuffer.Clear();
                            _temporaryBuffer.Append(possibleMatch);
                        }

                        _cursor = cursorPos;
                        ConsumeNextInputChar();
                            
                        if (!_currentInputChar.TryGet(out currChar)) break;
                        if (_nextInputChar.TryGet(out var nextChar))
                        {
                            if (ConsumedAsPartOfAnAttribute() && currChar != ';' &&
                                (nextChar == '=' || Codepoint.Get(nextChar).IsAsciiAlphanumeric()))
                            {
                                FlushCodepointsConsumedAsCharacterReference();
                                SwitchToState(_returnState);
                                return;
                            }
                        }

                        if (currChar != ';')
                        {
                            ParseError(HtmlParseError.MissingSemicolonAfterCharacterReference);
                        }

                        var namedCharReference = _temporaryBuffer.ToString();
                        _temporaryBuffer.Clear();
                        _temporaryBuffer.Append(
                            NamedCharacterReference.GetCodepointsOfNamedCharacterReference(namedCharReference));

                        FlushCodepointsConsumedAsCharacterReference();
                        SwitchToState(_returnState);
                            
                        return;
                    }
                    FlushCodepointsConsumedAsCharacterReference();
                    SwitchToState(HtmlTokenizerState.AmbiguousAmpersand);
                    break;
                
                case HtmlTokenizerState.AmbiguousAmpersand:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case ';':
                                ParseError(HtmlParseError.UnknownNamedCharacterReference);
                                ReconsumeInState(_returnState);
                                break;
                            default:
                                if (Codepoint.Get(currChar).IsAsciiAlphanumeric())
                                {
                                    if (ConsumedAsPartOfAnAttribute())
                                    {
                                        ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                    }
                                    else
                                    {
                                        CreateToken(new HtmlCharacterToken { Data = currChar.ToString() });
                                        EmitCurrentToken();
                                    }

                                    break;
                                }
                                ReconsumeInState(_returnState);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(_returnState);
                    break;

                case HtmlTokenizerState.NumericCharacterReference:
                    _characterReferenceCode = 0;
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case 'x':
                            case 'X':
                                _temporaryBuffer.Append(currChar);
                                SwitchToState(HtmlTokenizerState.HexadecimalCharacterReferenceStart);
                                break;
                            default:
                                ReconsumeInState(HtmlTokenizerState.DecimalCharacterReference);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.DecimalCharacterReferenceStart);
                    break;
                
                case HtmlTokenizerState.HexadecimalCharacterReferenceStart:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        if (Codepoint.Get(currChar).IsAsciiHexDigit())
                        {
                            ReconsumeInState(HtmlTokenizerState.HexadecimalCharacterReference);
                            return;
                        }
                    }
                    ParseError(HtmlParseError.AbsenceOfDigitsInNumericCharacterReference);
                    FlushCodepointsConsumedAsCharacterReference();
                    ReconsumeInState(_returnState);
                    break;
                
                case HtmlTokenizerState.DecimalCharacterReferenceStart:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        if (Codepoint.Get(currChar).IsAsciiDigit())
                        {
                            ReconsumeInState(HtmlTokenizerState.DecimalCharacterReference);
                            return;
                        }
                    }
                    ParseError(HtmlParseError.AbsenceOfDigitsInNumericCharacterReference);
                    FlushCodepointsConsumedAsCharacterReference();
                    ReconsumeInState(_returnState);
                    break;
                
                case HtmlTokenizerState.HexadecimalCharacterReference:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        var cp = Codepoint.Get(currChar);
                        if (cp.IsAsciiDigit())
                        {
                            _characterReferenceCode *= 16;
                            _characterReferenceCode += cp.Subtract(0x0030);
                            return;
                        }
                        if (cp.IsAsciiUpperHexDigit())
                        {
                            _characterReferenceCode *= 16;
                            _characterReferenceCode += cp.Subtract(0x0037);
                            return;
                        }
                        if (cp.IsAsciiLowerHexDigit())
                        {
                            _characterReferenceCode *= 16;
                            _characterReferenceCode += cp.Subtract(0x0057);
                            return;
                        }

                        if (currChar == ';')
                        {
                            SwitchToState(HtmlTokenizerState.NumericCharacterReferenceEnd);
                            return;
                        }
                    }
                    ParseError(HtmlParseError.MissingSemicolonAfterCharacterReference);
                    ReconsumeInState(HtmlTokenizerState.NumericCharacterReferenceEnd);
                    break;
                
                case HtmlTokenizerState.DecimalCharacterReference:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        var cp = Codepoint.Get(currChar);
                        if (cp.IsAsciiDigit())
                        {
                            _characterReferenceCode *= 10;
                            _characterReferenceCode += cp.Subtract(0x0030);
                            return;
                        }

                        if (currChar == ';')
                        {
                            SwitchToState(HtmlTokenizerState.NumericCharacterReferenceEnd);
                            return;
                        }
                    }
                    ParseError(HtmlParseError.MissingSemicolonAfterCharacterReference);
                    ReconsumeInState(HtmlTokenizerState.NumericCharacterReferenceEnd);
                    break;
                
                case HtmlTokenizerState.NumericCharacterReferenceEnd:

                    var charRefCp = Codepoint.Get(_characterReferenceCode);
                    
                    if (_characterReferenceCode == Codepoint.NULL)
                    {
                        ParseError(HtmlParseError.NullCharacterReference);
                        _characterReferenceCode = Codepoint.REPLACEMENT_CHARACTER;
                    }else if (_characterReferenceCode > 0x10FFFF)
                    {
                        ParseError(HtmlParseError.CharacterReferenceOutsideUnicodeRange);
                        _characterReferenceCode = Codepoint.REPLACEMENT_CHARACTER;
                    }else if (charRefCp.IsSurrogate())
                    {
                        ParseError(HtmlParseError.SurrogateCharacterReference);
                        _characterReferenceCode = Codepoint.REPLACEMENT_CHARACTER;
                    }else if (charRefCp.IsNonCharacter())
                    {
                        ParseError(HtmlParseError.NonCharacterCharacterReference);
                    }else if (_characterReferenceCode == Codepoint.CR ||
                              charRefCp.IsControl() && !charRefCp.IsAsciiWhitespace())
                    {
                        ParseError(HtmlParseError.ControlCharacterReference);
                        _characterReferenceCode = _characterReferenceCode switch
                        {
                            0x80 => 0x20AC,
                            0x82 => 0x201A,
                            0x83 => 0x0192,
                            0x84 => 0x201E,
                            0x85 => 0x2026,
                            0x86 => 0x2020,
                            0x87 => 0x2021,
                            0x88 => 0x02C6,
                            0x89 => 0x2030,
                            0x8A => 0x0160,
                            0x8B => 0x2039,
                            0x8C => 0x0152,
                            0x8E => 0x017D,
                            0x91 => 0x2018,
                            0x92 => 0x2019,
                            0x93 => 0x201C,
                            0x94 => 0x201D,
                            0x95 => 0x2022,
                            0x96 => 0x2013,
                            0x97 => 0x2014,
                            0x98 => 0x02DC,
                            0x99 => 0x2122,
                            0x9A => 0x0161,
                            0x9B => 0x203A,
                            0x9C => 0x0153,
                            0x9E => 0x017E,
                            0x9F => 0x0178,
                            _ => _characterReferenceCode
                        };
                    }
                    
                    _temporaryBuffer.Clear();
                    _temporaryBuffer.Append((char) _characterReferenceCode);
                    FlushCodepointsConsumedAsCharacterReference();
                    SwitchToState(_returnState);
                    break;
                
                default:
                    if(_debug) Console.WriteLine($"[DBG] Unhandled state: {Enum.GetName(_state)}");
                    EmitEndOfFileToken();
                    break;
            }
        }

        private bool ConsumedAsPartOfAnAttribute()
        {
            return _returnState is
                HtmlTokenizerState.AttributeValueDoubleQuoted
                or HtmlTokenizerState.AttributeValueSingleQuoted
                or HtmlTokenizerState.AttributeValueUnquoted;
        }

        private void FlushCodepointsConsumedAsCharacterReference()
        {
            if (ConsumedAsPartOfAnAttribute())
            {
                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(_temporaryBuffer);
                
            }
            else
            {
                foreach (var c in _temporaryBuffer.ToString())
                {
                    CreateToken(new HtmlCharacterToken {Data = c.ToString()});
                    EmitCurrentToken();
                }   
            }
        }

        private void EmitEndOfFileToken()
        {
            CreateToken(new HtmlEndOfFileToken());
            EmitCurrentToken();
            _isEof = true;
        }
        
        private void EmitCurrentToken()
        {
            // Remove "removed" attributes from HtmlTagTokens (e.g. duplicated attributes)
            if (_currentToken is HtmlTagToken tagToken)
            {
                switch (tagToken)
                {
                    case HtmlStartTagToken startTagToken:
                    {
                        // if (startTagToken.SelfClosing && !startTagToken.SelfClosingAcknowledged)
                        // {
                        //     ParseError(HtmlParseError.NonVoidHtmlElementStartTagWithTrailingSolidus);
                        // }

                        break;
                    }
                    case HtmlEndTagToken endTagToken:
                    {
                        if (endTagToken.Attributes.Count > 0)
                        {
                            ParseError(HtmlParseError.EndTagWithAttributes);
                        }

                        if (endTagToken.SelfClosing)
                        {
                            ParseError(HtmlParseError.EndTagWithTrailingSolidus);
                        }

                        break;
                    }
                }

                tagToken.Attributes.RemoveAll(it => it.Removed);
            }
            _tokens.Enqueue(_currentToken);
            if(_debug) Console.WriteLine($"[DBG] Emitting Token: {_currentToken}");
        }

        private void CreateToken(HtmlToken token)
        {
            _currentToken = token;
            if(_debug) Console.WriteLine($"[DBG] Creating Token: {_currentToken}");
        }
        
        private void IgnoreCharacter()
        {
            RunTokenizationStep();
        }

        private void ConsumeNextInputChar()
        {
            if (_reconsume && _cursor > 1)
            {
                _reconsume = false;
                if (!_debug) return;
                
                Console.WriteLine("[DBG] Reconsume");
                Debug_PrintChars();
                return;
            }
            _currentInputChar = _cursor <= 0 || _cursor - 1 >= _input.Length ? Optional<char>.Empty() : Optional<char>.Of(_input[_cursor-1]);
            _nextInputChar = _cursor >= _input.Length ? Optional<char>.Empty() : Optional<char>.Of(_input[_cursor++]);
            if (!_nextInputChar.HasValue)
            {
                _cursor++;
            }
            if(_debug) Debug_PrintChars();
        }

        private void ConsumeString(string consume)
        {
            if(_debug) Console.WriteLine("[DBG] ConsumeString START");
            foreach (var _ in consume)
            {
                ConsumeNextInputChar();
            }
            if(_debug) Console.WriteLine("[DBG] ConsumeString END");
        }
        
        private void ConsumeStringFromCurrentInputCharacter(string consume)
        {
            if(_debug) Console.WriteLine("[DBG] ConsumeString START");
            if(consume.Length <= 1) return;
            
            foreach (var _ in consume[1..])
            {
                ConsumeNextInputChar();
            }
            if(_debug) Console.WriteLine("[DBG] ConsumeString END");
        }

        private bool CheckForStringCaseSensitive(string peek)
        {
            if (peek.Length + _cursor > _input.Length) return false;
            
            var match = true;
            for (var i = 0; i < peek.Length; i++)
            {
                match = CheckForCharCaseSensitiveAt(peek[i], i);
            }
            return match;
        }
        
        private bool CheckForStringCaseInsensitive(string peek)
        {
            if (peek.Length + _cursor > _input.Length) return false;
            
            var match = true;
            for (var i = 0; i < peek.Length; i++)
            {
                match = CheckForCharCaseInsensitiveAt(peek[i], i);
            }
            return match;
        }
        
        private bool CheckForStringCaseInsensitiveFromCurrentInputCharacter(string peek)
        {
            if (peek.Length - 2 + _cursor > _input.Length) return false;
            if (!_currentInputChar.TryGet(out var currChar)) return false;
            
            var match = char.ToLower(currChar) == char.ToLower(peek[0]);
            for (var i = 0; i < peek.Length - 1; i++)
            {
                if (!match) break;
                match = CheckForCharCaseInsensitiveAt(peek[i+1], i);
            }
            return match;
        }

        private bool CheckForCharCaseSensitiveAt(char c, int offset)
        {
            var peekCursor = _cursor - 1 + offset;
            if(_debug) Console.WriteLine($"[DBG] PEEK_CaseSensitive | Expected: {c} Actual: {_input[peekCursor]}");
            return peekCursor < _input.Length && _input[peekCursor] == c;
        }
        
        private bool CheckForCharCaseInsensitiveAt(char c, int offset)
        {
            var peekCursor = _cursor - 1 + offset;
            if(_debug) Console.WriteLine($"[DBG] PEEK_CaseInsensitive | Expected: |{c}| Actual: |{_input[peekCursor]}|");
            return peekCursor < _input.Length && char.ToLower(_input[peekCursor]) == char.ToLower(c);
        }

        private void SwitchToState(HtmlTokenizerState state)
        {
            if(_debug) Console.WriteLine($"[DBG] StateSwitch {Enum.GetName(_state)} -> {Enum.GetName(state)}");
            _state = state;
            RunTokenizationStep();
        }

        private void ReconsumeInState(HtmlTokenizerState state)
        {
            _reconsume = true;
            SwitchToState(state);
        }

        private void AppendCharToDoctypeTokenName(char c)
        {
            if (_currentToken is HtmlDoctypeToken token)
            {
                if (token.Name.TryGet(out var tokenName))
                {
                    token.Name = Optional<string>.Of(tokenName + c);
                    return;
                }
            }
            
            ShouldNotBeReachable();
        }
        
        private void ParseError(HtmlParseError error)
        {
            Console.Error.WriteLine($"Tokenizer parse error: {error}");
            _errors.Enqueue(error);
        }

        private void CheckTagAttributes()
        {
            if (_currentToken is not HtmlTagToken token) return;
            var attributeNames = new List<string>();
            foreach (var attribute in token.Attributes)
            {
                if (attributeNames.Contains(attribute.Key))
                {
                    ParseError(HtmlParseError.DuplicateAttribute);
                    attribute.Removed = true;
                    return;
                }
                attributeNames.Add(attribute.Key);
            }
        }
        
        // DEBUG ===============

        private void Debug_PrintChars()
        {
            const string empty = "<<EMPTY>>";
            const string space = "<<SPACE>>";
            
            _nextInputChar.TryGet(out var nextChar);
            _currentInputChar.TryGet(out var currChar);
            Console.WriteLine($"[DBG] Consume | Current: {(_currentInputChar.HasValue ? Regex.Escape(currChar.ToString().Replace(" ", space)) : empty)} Next: {(_nextInputChar.HasValue ? Regex.Escape(nextChar.ToString().Replace(" ", space)) : empty)}");
        }

        private void ShouldNotBeReachable()
        {
            if (_debug) throw new UnreachableException();
        }
    }
}