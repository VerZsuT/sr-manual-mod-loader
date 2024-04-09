namespace sr_mml_lang.lexer.operators;

enum Operator : byte {
  OpenBrace,
  OpenParen,
  CloseBrace,
  CloseParen,
  Minus,
  Equals,
  Comma
}

static class OperatorExtensions {
  public static bool IsOperator(this char ch) {
    return IsOperator(ch.ToString());
  }

  public static bool IsOperator(this string str) {
    foreach (var @operator in Enum.GetValues<Operator>()) {
      if (@operator.ToString() == str) {
        return true;
      }
    }
    return false;
  }

  public static string ToString(this Operator @operator) {
    return @operator switch {
      Operator.OpenParen => "(",
      Operator.OpenBrace => "{",
      Operator.CloseParen => ")",
      Operator.CloseBrace => "}",
      Operator.Minus => "-",
      Operator.Equals => "=",
      Operator.Comma => ",",
      _ => throw new ArgumentOutOfRangeException(nameof(@operator))
    };
  }

  public static bool IsOperator(this Token token, Operator @operator) {
    return token.IsOperator() && token.Value == @operator.ToString();
  }
}
