namespace sr_mml_lang.lexer;

class LexException(string message) : Exception(message);

class UnclosedQuoteException(string? str, in TokenList tokens)
  : LexException($"Unclosed quote in \"" +
    $"{tokens.Inspect()} '" +
    $"{str?[..Math.Min(str.Length, 10)] ?? ""}\""
  );

class UnexpectedCharExteption(char? ch, in TokenList tokens)
  : LexException($"Unexpected character \"{ch}\" in \"{tokens.Inspect()} {ch}\"");

class InvalidNumberException(string str, in TokenList tokens)
  : LexException($"Invalid number \"{str}\" in \"{tokens.Inspect()} {str}\"");
