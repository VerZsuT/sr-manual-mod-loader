using System.Diagnostics;

namespace sr_mml_lang.lexer;

[DebuggerDisplay("TokenList")]
class TokenList : List<Token> {
  public bool HasItems => Count > 0;

  public string Inspect(int count = 2) {
    List<Token> list = [];

    if (Count == 0) {
      return "";
    }

    for (int i = count; i >= 1; --i) {
      if (Count < i) {
        continue;
      }
      list.Add(this[^i]);
    }

    return string.Join(
      " ",
      from token in list
      select token.Value
    );
  }

  public Token? GetAt(int index) {
    return (index >= Count)
    ? null
    : this[index];
  }
}

sealed partial class Token {
  public static Token NewKeyword(string value) {
    return new(TokenType.Keyword, value);
  }

  public static Token NewOperator(char value) {
    return NewOperator(value.ToString());
  }
  public static Token NewOperator(string value) {
    return new(TokenType.Operator, value);
  }

  public static Token NewID(string value) {
    return new(TokenType.Identifier, value);
  }

  public static Token NewString(string? value = null) {
    return new(TokenType.String, value ?? "");
  }

  public static Token NewNumber(string value) {
    return new(TokenType.Number, value);
  }

  public static Token NewBoolean(string value) {
    return new(TokenType.Boolean, value);
  }
}

[DebuggerDisplay("Token type: {Type} value: {Value}")]
sealed partial class Token(TokenType type, object value) {
  public TokenType Type { get; } = type;
  public string Value { get; } = value.ToString() ?? "";

  public static bool operator !=(Token left, Token right) {
    return !(left == right);
  }

  public static bool operator !=(Token left, char right) {
    return !(left == right);
  }

  public static bool operator !=(Token left, string right) {
    return !(left == right);
  }

  public static bool operator ==(Token left, Token right) {
    return left.Value == right.Value;
  }

  public static bool operator ==(Token left, char right) {
    return left.Value == right.ToString();
  }

  public static bool operator ==(Token left, string right) {
    return left.Value == right;
  }

  public override bool Equals(object? obj) {
    return obj switch {
      string str => this == str,
      char ch => this == ch,
      Token token => this == token,
      _ => base.Equals(obj),
    };
  }

  public override int GetHashCode() {
    return base.GetHashCode();
  }
}
