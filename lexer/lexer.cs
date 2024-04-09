using sr_mml_lang.lexer.keywords;
using sr_mml_lang.lexer.operators;
using System.Globalization;

namespace sr_mml_lang.lexer;

class Lexer() {
  readonly TokenList tokens = [];
  string source = "";

  int currentIndex = 0;

  char? CurrentChar => (currentIndex >= source.Length)
    ? null
    : source[currentIndex];

  char? NextChar => (currentIndex + 1 >= source.Length)
    ? null
    : source[currentIndex + 1];

  bool HasNext => NextChar is not null;

  public TokenList Lex(string str) {
    currentIndex = 0;
    source = str;
    tokens.Clear();

    while (HasNext) {
      LexNext();
    }
    return tokens;
  }

  void LexNext() {
    int index = currentIndex;
    Func<bool>[] lexers = [
      LexComment,
      LexKeyword,
      LexBoolean,
      LexString,
      LexNumber,
      LexOperator,
      LexIdentifier,
    ];

    for (int i = 0; i < lexers.Length; ++i) {
      var lexer = lexers[i];

      SkipSpace();
      if (lexer()) {
        i = -1;
      }
    }

    if (index == currentIndex && HasNext) {
      throw new UnexpectedCharExteption(CurrentChar, tokens);
    }
  }

  bool LexString() {
    string? str;

    if (CurrentChar != '\'') {
      return false;
    }

    Next(); // skip start '
    str = GetCharSequence(ch => ch != '\'');
    if (CurrentChar != '\'') {
      throw new UnclosedQuoteException(str, tokens);
    }

    Next(); // skip end '
    tokens.Add(Token.NewString(str));

    return true;
  }

  bool LexBoolean() {
    string? str = GetCharSequence(ch => ch.IsBooleanPart(), word => word.IsBoolean());

    if (str is null) {
      return false;
    }

    tokens.Add(Token.NewBoolean(str));
    return true;
  }

  bool LexNumber() {
    string? str = GetCharSequence(ch => ch.IsNumberPart());

    if (str is null) {
      return false;
    }

    tokens.Add(Token.NewNumber(ParseDoubleStr(str)));
    return true;
  }

  bool LexComment() {
    return LexLineComment() || LexMultilineComment();

    bool LexLineComment() {
      string? commentStart = GetCharSequence(ch => ch == '/', word => word.StartsWith("//"));

      if (commentStart is null) {
        return false;
      }

      GetCharSequence(ch => ch != '\n');
      return true;
    }

    bool LexMultilineComment() {
      string? commentStart = GetCharSequence(ch => ch == '/' || ch == '*', word => word.StartsWith("/*"));
      bool ended = false;

      if (commentStart is null) {
        return false;
      }

      do {
        GetCharSequence(ch => ch != '*');
        Next();
        ended = CurrentChar == '/';
      }
      while (!ended && CurrentChar is not null);

      Next();
      return true;
    }
  }

  bool LexKeyword() {
    string? keyword = GetCharSequence(ch => ch.IsIDPart(), word => word.IsKeyword());

    if (keyword is null) {
      return false;
    }

    tokens.Add(Token.NewKeyword(keyword));
    return true;
  }

  bool LexIdentifier() {
    string? id = GetCharSequence(ch => ch.IsIDPart());

    if (id is null) {
      return false;
    }

    tokens.Add(Token.NewID(id));
    return true;
  }

  bool LexOperator() {
    string? operators = GetCharSequence(ch => ch.IsOperator());

    if (operators is null || operators.Length == 0) {
      return false;
    }

    foreach (char oper in operators) {
      tokens.Add(Token.NewOperator(oper));
    }

    return true;
  }

  string? GetCharSequence(Func<char, bool> charChecker, Func<string, bool>? wordChecker = null) {
    int prevIndex = currentIndex;
    string word = "";
    char? current = CurrentChar;

    wordChecker ??= str => str.Length != 0;
    while (current is not null && charChecker((char)current)) {
      word += current;
      current = Next();
    }

    if (!wordChecker(word)) {
      currentIndex = prevIndex;
      return null;
    }

    return word;
  }

  void SkipSpace() {
    GetCharSequence(char.IsWhiteSpace);
  }

  char? Next() {
    ++currentIndex;
    return CurrentChar;
  }

  string ParseDoubleStr(string str) {
    var cultureInfo = CultureInfo.GetCultureInfo("en-US");

    return !double.TryParse(str, cultureInfo, out double number)
      ? throw new InvalidNumberException(str, tokens)
      : number.ToString(cultureInfo);
  }
}
