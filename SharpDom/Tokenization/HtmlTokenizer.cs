using System;
using System.Collections.Generic;
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
        private bool _reconsume;
        private bool _isEof;
        
        private readonly bool _debug;

        private HtmlTokenizerState _state = HtmlTokenizerState.Data;
        
        private Optional<char> _nextInputChar = Optional<char>.Empty();
        private Optional<char> _currentInputChar = Optional<char>.Empty();

        private HtmlToken _currentToken;

        public HtmlTokenizer(string input, bool debugMode = false)
        {
            _input = InputStreamPreprocessor.PreprocessInputStream(input);
            _debug = debugMode;
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
                                break;
                            case '<':
                                SwitchToState(HtmlTokenizerState.TagOpen);
                                break;
                            case Codepoint.NULL:
                                ParseError("unexpected-null-character");
                                CreateToken(new HtmlCharacterToken {Data = currChar.ToString()});
                                EmitCurrentToken();
                                // RunTokenizationStep();
                                break;
                            default:
                                CreateToken(new HtmlCharacterToken {Data = currChar.ToString()});
                                EmitCurrentToken();
                                // RunTokenizationStep();
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
                                ParseError("unexpected-question-mark-instead-of-tag-name");
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
                                ParseError("invalid-first-character-of-tag-name");
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
                
                case HtmlTokenizerState.MarkupDeclarationOpen:
                    if (PeekCaseSensitive("--"))
                    {
                        ConsumeString("--");
                        CreateToken(new HtmlCommentToken {Data = ""});
                        SwitchToState(HtmlTokenizerState.CommentStart);
                        return;
                    }

                    if (PeekCaseInsensitive("DOCTYPE"))
                    {
                        ConsumeString("DOCTYPE");
                        SwitchToState(HtmlTokenizerState.Doctype);
                        return;
                    }
                    ParseError("incorrectly-opened-comment");
                    CreateToken(new HtmlCommentToken { Data = "" });
                    SwitchToState(HtmlTokenizerState.BogusComment);
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
                                ParseError("missing-whitespace-before-doctype-name");
                                ReconsumeInState(HtmlTokenizerState.BeforeDoctypeName);
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-doctype");
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
                    }
                    ParseError("eof-in-doctype");
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
                    ParseError("eof-in-doctype");
                    ((HtmlDoctypeToken) _currentToken).ForceQuirks = true;
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
                                ParseError("unexpected-null-character");
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
                    ParseError("eof-in-tag");
                    EmitEndOfFileToken();
                    break;
                
                case HtmlTokenizerState.EndTagOpen:
                    ConsumeNextInputChar();
                    if (_currentInputChar.TryGet(out currChar))
                    {
                        switch (currChar)
                        {
                            case '>':
                                ParseError("missing-end-tag-name");
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                if (Codepoint.Get(currChar).IsAsciiAlpha())
                                {
                                    CreateToken(new HtmlEndTagToken {TagName = ""});
                                    ReconsumeInState(HtmlTokenizerState.TagName);
                                    return;
                                }
                                ParseError("invalid-first-character-of-tag-name");
                                CreateToken(new HtmlCommentToken {Data = ""});
                                ReconsumeInState(HtmlTokenizerState.BogusComment);
                                break;
                        }
                        return;
                    }
                    ParseError("eof-before-tag-name");
                    CreateToken(new HtmlCharacterToken {Data = '<'.ToString()});
                    EmitCurrentToken();
                    CreateToken(new HtmlCharacterToken {Data = '/'.ToString()});
                    EmitCurrentToken();
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
                                ParseError("unexpected-equals-sign-before-attribute-name");
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
                                ReconsumeInState(HtmlTokenizerState.AfterAttributeName);
                                break;
                            case '=':
                                SwitchToState(HtmlTokenizerState.BeforeAttributeValue);
                                break;
                            case Codepoint.NULL:
                                ParseError("unexpected-null-character");
                                ((HtmlTagToken) _currentToken).CurrentAttribute.KeyBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            case '"':
                            case Codepoint.APOSTROPHE:
                            case '<':
                                ParseError("unexpected-character-in-attribute-name");
                                ((HtmlTagToken) _currentToken).CurrentAttribute.KeyBuilder.Append(currChar);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.KeyBuilder.Append(Codepoint.Get(currChar).IsAsciiUpperAlpha() ? char.ToLower(currChar) : currChar);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.AfterAttributeName);
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
                                ParseError("missing-attribute-value");
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
                                // TODO: Set return state
                                SwitchToState(HtmlTokenizerState.CharacterReference);
                                break;
                            case Codepoint.NULL:
                                ParseError("unexpected-null-character");
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-tag");
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
                                // TODO: Set return state
                                SwitchToState(HtmlTokenizerState.CharacterReference);
                                break;
                            case Codepoint.NULL:
                                ParseError("unexpected-null-character");
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-tag");
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
                                // TODO: Set return state
                                SwitchToState(HtmlTokenizerState.CharacterReference);
                                break;
                            case '>':
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            case Codepoint.NULL:
                                ParseError("unexpected-null-character");
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(Codepoint.REPLACEMENT_CHARACTER);
                                break;
                            case '"':
                            case Codepoint.APOSTROPHE:
                            case '<':
                            case '=':
                            case Codepoint.GRAVE_ACCENT:
                                ParseError("unexpected-character-in-unquoted-attribute-value");
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                            default:
                                ((HtmlTagToken) _currentToken).CurrentAttribute.ValueBuilder.Append(currChar);
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-tag");
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
                                ParseError("missing-whitespace-between-attributes");
                                ReconsumeInState(HtmlTokenizerState.BeforeAttributeName);
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-tag");
                    EmitEndOfFileToken();
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
                                ParseError("abrupt-closing-of-empty-comment");
                                EmitCurrentToken();
                                SwitchToState(HtmlTokenizerState.Data);
                                break;
                            default:
                                ReconsumeInState(HtmlTokenizerState.CommentState);
                                break;
                        }
                        return;
                    }
                    ReconsumeInState(HtmlTokenizerState.CommentState);
                    break;
                
                case HtmlTokenizerState.CommentState:
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
                                ParseError("unexpected-null-character");
                                ((HtmlCommentToken) _currentToken).Data += Codepoint.REPLACEMENT_CHARACTER;
                                break;
                            default:
                                ((HtmlCommentToken) _currentToken).Data += currChar;
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-comment");
                    EmitCurrentToken();
                    EmitEndOfFileToken();
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
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-comment");
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
                                ReconsumeInState(HtmlTokenizerState.CommentState);
                                break;
                        }
                        return;
                    }
                    ParseError("eof-in-comment");
                    EmitCurrentToken();
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
                                ParseError("unexpected-null-character");
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
                
                default:
                    if(_debug) Console.WriteLine($"[DBG] Unhandled state: {Enum.GetName(_state)}");
                    EmitEndOfFileToken();
                    break;
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
            _currentInputChar = _nextInputChar;
            _nextInputChar = _cursor >= _input.Length ? Optional<char>.Empty() : Optional<char>.Of(_input[_cursor++]);
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

        private bool PeekCaseSensitive(string peek)
        {
            if (peek.Length + _cursor > _input.Length) return false;
            
            var match = true;
            for (var i = 0; i < peek.Length; i++)
            {
                match = PeekCharCaseSensitive(peek[i], i);
            }
            return match;
        }
        
        private bool PeekCaseInsensitive(string peek)
        {
            if (peek.Length + _cursor > _input.Length) return false;
            
            var match = true;
            for (var i = 0; i < peek.Length; i++)
            {
                match = PeekCharCaseInsensitive(peek[i], i);
            }
            return match;
        }

        private bool PeekCharCaseSensitive(char c, int offset)
        {
            var peekCursor = _cursor - 1 + offset;
            if(_debug) Console.WriteLine($"[DBG] PEEK_CaseSensitive | Expected: {c} Actual: {_input[peekCursor]}");
            return peekCursor < _input.Length && _input[peekCursor] == c;
        }
        
        private bool PeekCharCaseInsensitive(char c, int offset)
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
        // ===============

        private void ParseError(string errorType)
        {
            Console.Error.WriteLine($"Tokenizer parse error: {errorType}");
        }
        
        private void Debug_PrintChars()
        {
            const string empty = "<<EMPTY>>";
            
            _nextInputChar.TryGet(out var nextChar);
            _currentInputChar.TryGet(out var currChar);
            Console.WriteLine($"[DBG] Consume | Current: {(_currentInputChar.HasValue ? Regex.Escape(currChar.ToString()) : empty)} Next: {(_nextInputChar.HasValue ? Regex.Escape(nextChar.ToString()) : empty)}");
        }

        private void ShouldNotBeReachable()
        {
            if (_debug) throw new UnreachableException();
        }
    }
}