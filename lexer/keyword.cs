namespace sr_mml_lang.lexer.keywords;

enum Keyword : byte {
  File,
  Include,
  Use,
  Const,
  Preset,
  Change,
  Change_Add,
  Add,
  Remove,
  Set,
  All,
  At
}

static class KeywordExtensions {
  public static bool IsKeyword(this string str) {
    return Enum.TryParse<Keyword>(str, ignoreCase: true, out _);
  }

  public static string ToString(this Keyword keyword) {
    return Enum.GetName(typeof(Keyword), keyword)!.ToLower();
  }

  public static bool IsKeyword(this Token token, Keyword keyword) {
    return token.IsKeyword() && token.Value == keyword.ToString();
  }
}
