namespace sr_mml_lang.lexer;

enum TokenType {
  Keyword,
  String,
  Number,
  Operator,
  Identifier,
  Boolean
}

static class TokenTypeExtentions {
  public static bool IsBoolean(this string str) {
    return str is "true" or "false";
  }

  public static bool IsNumberPart(this char ch) {
    return char.IsDigit(ch) || ch == '.';
  }

  public static bool IsBooleanPart(this char ch) {
    char[] chars = [
      't', 'r', 'u', 'e',
      'f', 'a', 'l', 's'
    ];
    return chars.Contains(ch);
  }

  public static bool IsKeywordPart(this char ch) {
    return ch.IsIDPart();
  }

  public static bool IsIDPart(this char ch) {
    return char.IsLetterOrDigit(ch) || ch == '_';
  }

  public static bool TypeIs(this Token token, IEnumerable<TokenType> types) {
    return types.Contains(token.Type);
  }

  public static bool TypeIs(this Token token, TokenType type) {
    return token.Type == type;
  }

  public static bool IsKeyword(this Token token) {
    return token.Type == TokenType.Keyword;
  }

  public static bool IsString(this Token token) {
    return token.Type == TokenType.String;
  }

  public static bool IsNumber(this Token token) {
    return token.Type == TokenType.Number;
  }

  public static bool IsOperator(this Token token) {
    return token.Type == TokenType.Operator;
  }

  public static bool IsIdentifier(this Token token) {
    return token.Type == TokenType.Identifier;
  }

  public static bool IsBoolean(this Token token) {
    return token.Type == TokenType.Boolean;
  }
}
